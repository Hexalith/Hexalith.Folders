using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authorization;

namespace Hexalith.Folders.Server;

public sealed class FolderDomainProcessor(
    FolderArchiveTenantGate archiveGate,
    ILayeredFolderAuthorizationResultAccessor authorizationAccessor,
    IFolderArchiveAclEvidenceProvider archiveAclEvidenceProvider,
    IFolderArchivePolicyEvidenceProvider archivePolicyEvidenceProvider) : IDomainProcessor
{
    private const string ArchiveFolderCommandType = "Hexalith.Folders.Commands.ArchiveFolder";

    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly FolderArchiveTenantGate _archiveGate = archiveGate ?? throw new ArgumentNullException(nameof(archiveGate));
    private readonly ILayeredFolderAuthorizationResultAccessor _authorizationAccessor =
        authorizationAccessor ?? throw new ArgumentNullException(nameof(authorizationAccessor));
    private readonly IFolderArchiveAclEvidenceProvider _archiveAclEvidenceProvider =
        archiveAclEvidenceProvider ?? throw new ArgumentNullException(nameof(archiveAclEvidenceProvider));
    private readonly IFolderArchivePolicyEvidenceProvider _archivePolicyEvidenceProvider =
        archivePolicyEvidenceProvider ?? throw new ArgumentNullException(nameof(archivePolicyEvidenceProvider));

    public async Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!string.Equals(command.Domain, FoldersServerModule.DomainName, StringComparison.Ordinal))
        {
            return Rejection(command, FolderResultCode.UnsupportedCommandType, null);
        }

        return string.Equals(command.CommandType, ArchiveFolderCommandType, StringComparison.Ordinal)
            ? await ProcessArchiveAsync(command).ConfigureAwait(false)
            : Rejection(command, FolderResultCode.UnsupportedCommandType, null);
    }

    private async Task<DomainResult> ProcessArchiveAsync(CommandEnvelope envelope)
    {
        ArchiveFolderPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ArchiveFolderPayload>(envelope.Payload, PayloadJsonOptions);
        }
        catch (JsonException)
        {
            return Rejection(envelope, FolderResultCode.ValidationFailed, null);
        }

        if (payload is null)
        {
            return Rejection(envelope, FolderResultCode.ValidationFailed, null);
        }

        LayeredFolderAuthorizationAllowedContext? allowed = _authorizationAccessor.Current?.AllowedContext;
        string organizationId = allowed?.OrganizationId ?? string.Empty;
        string taskId = envelope.Extensions is not null
            && envelope.Extensions.TryGetValue("taskId", out string? extensionTaskId)
            ? extensionTaskId
            : string.Empty;

        ArchiveFolder command = new(
            envelope.TenantId,
            organizationId,
            envelope.AggregateId,
            payload.RequestSchemaVersion ?? string.Empty,
            payload.ArchiveReasonCode ?? string.Empty,
            envelope.UserId,
            envelope.CorrelationId,
            taskId,
            envelope.MessageId,
            PayloadTenantId: null,
            ClientTenantIds: new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["eventstore_envelope_tenant"] = envelope.TenantId,
            });

        TenantAccessAuthorizationResult tenantAccess = BuildTenantAccess(allowed);
        FolderArchiveAclEvidence aclEvidence = await _archiveAclEvidenceProvider
            .GetEvidenceAsync(command, CancellationToken.None)
            .ConfigureAwait(false);
        FolderArchivePolicyEvidence policyEvidence = await _archivePolicyEvidenceProvider
            .GetEvidenceAsync(command, CancellationToken.None)
            .ConfigureAwait(false);

        FolderResult result = _archiveGate.Handle(command, tenantAccess, aclEvidence, policyEvidence);
        return ToDomainResult(envelope, result);
    }

    private static TenantAccessAuthorizationResult BuildTenantAccess(LayeredFolderAuthorizationAllowedContext? allowed)
        => allowed is null
            ? new(
                TenantAccessOutcome.MalformedEvidence,
                "malformed_evidence",
                null,
                null,
                null,
                null,
                TenantProjectionFreshnessStatus.Unknown,
                "layered-authorization")
            : new(
                TenantAccessOutcome.Allowed,
                "allowed",
                allowed.AuthoritativeTenantId,
                allowed.FreshnessWatermark,
                null,
                null,
                MapFreshness(allowed),
                "layered-authorization");

    private static TenantProjectionFreshnessStatus MapFreshness(LayeredFolderAuthorizationAllowedContext allowed)
        => string.IsNullOrWhiteSpace(allowed.FreshnessWatermark)
            ? TenantProjectionFreshnessStatus.Unknown
            : TenantProjectionFreshnessStatus.Fresh;

    private static DomainResult ToDomainResult(CommandEnvelope envelope, FolderResult result)
        => result.Code switch
        {
            FolderResultCode.Accepted
                or FolderResultCode.Created
                or FolderResultCode.IdempotentReplay
                or FolderResultCode.AlreadyApplied => DomainResult.NoOp(),
            _ => Rejection(envelope, result.Code, result),
        };

    private static DomainResult Rejection(CommandEnvelope envelope, FolderResultCode code, FolderResult? result)
    {
        IRejectionEvent rejection = new FolderCommandRejected(
            code.ToString(),
            envelope.CommandType,
            result?.ManagedTenantId,
            result?.OrganizationId,
            result?.FolderId,
            result?.ActorPrincipalId,
            result?.CorrelationId ?? envelope.CorrelationId,
            result?.TaskId,
            result?.IdempotencyKey ?? envelope.MessageId);

        return DomainResult.Rejection([rejection]);
    }

    private sealed record ArchiveFolderPayload(
        string? RequestSchemaVersion,
        string? ArchiveReasonCode);
}
