using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Server.Authorization;

namespace Hexalith.Folders.Server;

public sealed partial class FolderDomainProcessor(
    FolderArchiveTenantGate archiveGate,
    RepositoryBackedFolderCreationService repositoryBackedCreationService,
    RepositoryBindingService repositoryBindingService,
    BranchRefPolicyConfigurationService branchRefPolicyConfigurationService,
    WorkspacePreparationService workspacePreparationService,
    WorkspaceLockAcquisitionService workspaceLockAcquisitionService,
    WorkspaceLockReleaseService workspaceLockReleaseService,
    WorkspaceFileMutationService workspaceFileMutationService,
    WorkspaceCommitService workspaceCommitService,
    ILayeredFolderAuthorizationResultAccessor authorizationAccessor,
    IFolderArchiveAclEvidenceProvider archiveAclEvidenceProvider,
    IFolderArchivePolicyEvidenceProvider archivePolicyEvidenceProvider) : IDomainProcessor
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    // Mirrors FoldersDomainServiceEndpoints.CanonicalSegmentRegex. Extension values
    // arriving on the /process boundary are not validated by the REST endpoint, so the
    // processor re-validates before propagating them into command/event metadata.
    [GeneratedRegex("^[a-z0-9._-]+$", RegexOptions.Compiled)]
    private static partial Regex CanonicalSegmentRegex();

    private readonly FolderArchiveTenantGate _archiveGate = archiveGate ?? throw new ArgumentNullException(nameof(archiveGate));
    private readonly RepositoryBackedFolderCreationService _repositoryBackedCreationService =
        repositoryBackedCreationService ?? throw new ArgumentNullException(nameof(repositoryBackedCreationService));
    private readonly RepositoryBindingService _repositoryBindingService =
        repositoryBindingService ?? throw new ArgumentNullException(nameof(repositoryBindingService));
    private readonly BranchRefPolicyConfigurationService _branchRefPolicyConfigurationService =
        branchRefPolicyConfigurationService ?? throw new ArgumentNullException(nameof(branchRefPolicyConfigurationService));
    private readonly WorkspacePreparationService _workspacePreparationService =
        workspacePreparationService ?? throw new ArgumentNullException(nameof(workspacePreparationService));
    private readonly WorkspaceLockAcquisitionService _workspaceLockAcquisitionService =
        workspaceLockAcquisitionService ?? throw new ArgumentNullException(nameof(workspaceLockAcquisitionService));
    private readonly WorkspaceLockReleaseService _workspaceLockReleaseService =
        workspaceLockReleaseService ?? throw new ArgumentNullException(nameof(workspaceLockReleaseService));
    private readonly WorkspaceFileMutationService _workspaceFileMutationService =
        workspaceFileMutationService ?? throw new ArgumentNullException(nameof(workspaceFileMutationService));
    private readonly WorkspaceCommitService _workspaceCommitService =
        workspaceCommitService ?? throw new ArgumentNullException(nameof(workspaceCommitService));
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

        if (!IsValidProcessEnvelope(command))
        {
            return Rejection(command, FolderResultCode.ValidationFailed, null);
        }

        if (string.Equals(command.CommandType, FoldersServerModule.ArchiveFolderCommandType, StringComparison.Ordinal))
        {
            return await ProcessArchiveAsync(command).ConfigureAwait(false);
        }

        if (string.Equals(command.CommandType, FoldersServerModule.CreateRepositoryBackedFolderCommandType, StringComparison.Ordinal))
        {
            return await ProcessCreateRepositoryBackedFolderAsync(command).ConfigureAwait(false);
        }

        if (string.Equals(command.CommandType, FoldersServerModule.BindRepositoryCommandType, StringComparison.Ordinal))
        {
            return await ProcessBindRepositoryAsync(command).ConfigureAwait(false);
        }

        if (string.Equals(command.CommandType, FoldersServerModule.ConfigureBranchRefPolicyCommandType, StringComparison.Ordinal))
        {
            return await ProcessConfigureBranchRefPolicyAsync(command).ConfigureAwait(false);
        }

        if (string.Equals(command.CommandType, FoldersServerModule.PrepareWorkspaceCommandType, StringComparison.Ordinal))
        {
            return await ProcessPrepareWorkspaceAsync(command).ConfigureAwait(false);
        }

        if (string.Equals(command.CommandType, FoldersServerModule.LockWorkspaceCommandType, StringComparison.Ordinal))
        {
            return await ProcessLockWorkspaceAsync(command).ConfigureAwait(false);
        }

        if (string.Equals(command.CommandType, FoldersServerModule.ReleaseWorkspaceLockCommandType, StringComparison.Ordinal))
        {
            return await ProcessReleaseWorkspaceLockAsync(command).ConfigureAwait(false);
        }

        if (string.Equals(command.CommandType, FoldersServerModule.MutateFilesCommandType, StringComparison.Ordinal))
        {
            return await ProcessMutateFilesAsync(command).ConfigureAwait(false);
        }

        if (string.Equals(command.CommandType, FoldersServerModule.CommitWorkspaceCommandType, StringComparison.Ordinal))
        {
            return await ProcessCommitWorkspaceAsync(command).ConfigureAwait(false);
        }

        return Rejection(command, FolderResultCode.UnsupportedCommandType, null);
    }

    private async Task<DomainResult> ProcessArchiveAsync(CommandEnvelope envelope)
    {
        ArchiveFolderPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ArchiveFolderPayload>(envelope.Payload, PayloadJsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // JsonException = malformed JSON / missing required field; NotSupportedException
            // = type-system mismatch. Both fail closed to the same safe 400 category.
            return Rejection(envelope, FolderResultCode.MalformedJsonPayload, null);
        }

        if (payload is null)
        {
            return Rejection(envelope, FolderResultCode.ValidationFailed, null);
        }

        // Layered authorization must have run upstream in the request handler and pinned the
        // authoritative tenant/actor/organization onto the scoped accessor. If it didn't, the
        // request should never have reached the processor — but fail closed to a malformed
        // evidence rejection if the contract is somehow violated.
        LayeredFolderAuthorizationAllowedContext? allowed = _authorizationAccessor.Current?.AllowedContext;
        if (allowed is null
            || string.IsNullOrWhiteSpace(allowed.OrganizationId)
            || string.IsNullOrWhiteSpace(allowed.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(allowed.ActorSafeIdentifier))
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        // taskId is a /process extension; sanitize the bytes that came over the wire before
        // they end up in command/event metadata or audit subjects.
        string taskId = ReadRequiredTaskId(envelope);

        ArchiveFolder command = new(
            // Source tenant/actor/organization from the verified layered-auth context, not the
            // raw envelope. Layered auth already compared envelope.TenantId against the
            // authenticated tenant; keep the envelope value only as a client-controlled
            // comparison input on ClientTenantIds.
            allowed.AuthoritativeTenantId,
            allowed.OrganizationId,
            envelope.AggregateId,
            payload.RequestSchemaVersion ?? string.Empty,
            payload.ArchiveReasonCode ?? string.Empty,
            allowed.ActorSafeIdentifier,
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

        FolderResult result;
        try
        {
            result = _archiveGate.Handle(command, tenantAccess, aclEvidence, policyEvidence);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Any unexpected exception from the gate (e.g. an evidence constructor invariant
            // violation) must fail closed without leaking type/stack metadata through the
            // gateway response or framework 500 path. OperationCanceledException is allowed
            // to propagate so cancellation semantics are preserved if a future async
            // dependency adds them.
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        return ToDomainResult(envelope, result);
    }

    private async Task<DomainResult> ProcessCreateRepositoryBackedFolderAsync(CommandEnvelope envelope)
    {
        CreateRepositoryBackedFolderPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<CreateRepositoryBackedFolderPayload>(envelope.Payload, PayloadJsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Rejection(envelope, FolderResultCode.MalformedJsonPayload, null);
        }

        if (payload is null
            || payload.FolderMetadata is null
            || payload.BranchRefPolicy is null
            || string.IsNullOrWhiteSpace(payload.FolderId)
            || !string.Equals(payload.FolderId, envelope.AggregateId, StringComparison.Ordinal))
        {
            return Rejection(envelope, FolderResultCode.ValidationFailed, null);
        }

        LayeredFolderAuthorizationAllowedContext? allowed = _authorizationAccessor.Current?.AllowedContext;
        if (allowed is null
            || string.IsNullOrWhiteSpace(allowed.OrganizationId)
            || string.IsNullOrWhiteSpace(allowed.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(allowed.ActorSafeIdentifier))
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        string taskId = ReadRequiredTaskId(envelope);
        Dictionary<string, string?> principalValues = new(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(envelope.UserId))
        {
            principalValues["eventstore_envelope_user"] = envelope.UserId;
        }

        RepositoryBackedFolderCreationRequest request = new(
            allowed.AuthoritativeTenantId,
            allowed.ActorSafeIdentifier,
            EventStoreClaimTransformEvidence.Allowed(
                allowed.AuthoritativeTenantId,
                allowed.ActorSafeIdentifier,
                [
                    RepositoryBackedFolderCreationService.ActionToken,
                    ProviderReadinessValidationService.ReadActionToken,
                ]),
            payload.FolderId,
            payload.RequestSchemaVersion ?? string.Empty,
            payload.RepositoryBindingId ?? payload.BranchRefPolicy.RepositoryBindingId ?? string.Empty,
            payload.ProviderBindingRef ?? string.Empty,
            payload.RepositoryProfileRef ?? string.Empty,
            payload.BranchRefPolicy.PolicyRef ?? string.Empty,
            payload.FolderMetadata.DisplayName ?? string.Empty,
            payload.CredentialScopeClass ?? "provider_binding",
            envelope.CorrelationId,
            taskId,
            envelope.MessageId,
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["eventstore_envelope_tenant"] = envelope.TenantId,
            },
            ClientControlledPrincipalValues: principalValues);

        FolderResult result;
        try
        {
            result = await _repositoryBackedCreationService.CreateAsync(request, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        return ToDomainResult(envelope, result);
    }

    private async Task<DomainResult> ProcessBindRepositoryAsync(CommandEnvelope envelope)
    {
        BindRepositoryPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<BindRepositoryPayload>(envelope.Payload, PayloadJsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Rejection(envelope, FolderResultCode.MalformedJsonPayload, null);
        }

        if (payload is null
            || payload.BranchRefPolicy is null
            || string.IsNullOrWhiteSpace(envelope.AggregateId))
        {
            return Rejection(envelope, FolderResultCode.ValidationFailed, null);
        }

        LayeredFolderAuthorizationAllowedContext? allowed = _authorizationAccessor.Current?.AllowedContext;
        if (allowed is null
            || string.IsNullOrWhiteSpace(allowed.OrganizationId)
            || string.IsNullOrWhiteSpace(allowed.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(allowed.ActorSafeIdentifier))
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        string taskId = ReadRequiredTaskId(envelope);
        Dictionary<string, string?> principalValues = new(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(envelope.UserId))
        {
            principalValues["eventstore_envelope_user"] = envelope.UserId;
        }

        BindRepositoryRequest request = new(
            allowed.AuthoritativeTenantId,
            allowed.ActorSafeIdentifier,
            EventStoreClaimTransformEvidence.Allowed(
                allowed.AuthoritativeTenantId,
                allowed.ActorSafeIdentifier,
                [
                    RepositoryBindingService.ActionToken,
                    ProviderReadinessValidationService.ReadActionToken,
                ]),
            envelope.AggregateId,
            payload.RequestSchemaVersion ?? string.Empty,
            payload.ProviderBindingRef ?? string.Empty,
            payload.ExternalRepositoryRef ?? string.Empty,
            payload.BranchRefPolicy.PolicyRef ?? string.Empty,
            "provider_binding",
            envelope.CorrelationId,
            taskId,
            envelope.MessageId,
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["eventstore_envelope_tenant"] = envelope.TenantId,
            },
            ClientControlledPrincipalValues: principalValues);

        FolderResult result;
        try
        {
            result = await _repositoryBindingService.BindAsync(request, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        return ToDomainResult(envelope, result);
    }

    private async Task<DomainResult> ProcessConfigureBranchRefPolicyAsync(CommandEnvelope envelope)
    {
        BranchRefPolicyPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<BranchRefPolicyPayload>(envelope.Payload, PayloadJsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Rejection(envelope, FolderResultCode.MalformedJsonPayload, null);
        }

        if (payload is null || string.IsNullOrWhiteSpace(envelope.AggregateId))
        {
            return Rejection(envelope, FolderResultCode.ValidationFailed, null);
        }

        LayeredFolderAuthorizationAllowedContext? allowed = _authorizationAccessor.Current?.AllowedContext;
        if (allowed is null
            || string.IsNullOrWhiteSpace(allowed.OrganizationId)
            || string.IsNullOrWhiteSpace(allowed.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(allowed.ActorSafeIdentifier))
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        string taskId = ReadRequiredTaskId(envelope);
        Dictionary<string, string?> principalValues = new(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(envelope.UserId))
        {
            principalValues["eventstore_envelope_user"] = envelope.UserId;
        }

        BranchRefPolicyConfigurationRequest request = new(
            allowed.AuthoritativeTenantId,
            allowed.ActorSafeIdentifier,
            EventStoreClaimTransformEvidence.Allowed(
                allowed.AuthoritativeTenantId,
                allowed.ActorSafeIdentifier,
                [
                    BranchRefPolicyConfigurationService.ActionToken,
                    ProviderReadinessValidationService.ReadActionToken,
                ]),
            envelope.AggregateId,
            payload.RequestSchemaVersion ?? string.Empty,
            payload.RepositoryBindingId ?? string.Empty,
            payload.PolicyRef ?? string.Empty,
            payload.DefaultRef ?? string.Empty,
            payload.AllowedRefPatterns ?? [],
            payload.ProtectedRefPatterns,
            envelope.CorrelationId,
            taskId,
            envelope.MessageId,
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["eventstore_envelope_tenant"] = envelope.TenantId,
            },
            ClientControlledPrincipalValues: principalValues);

        FolderResult result;
        try
        {
            result = await _branchRefPolicyConfigurationService.ConfigureAsync(request, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        return ToDomainResult(envelope, result);
    }

    private async Task<DomainResult> ProcessPrepareWorkspaceAsync(CommandEnvelope envelope)
    {
        PrepareWorkspacePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<PrepareWorkspacePayload>(envelope.Payload, PayloadJsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Rejection(envelope, FolderResultCode.MalformedJsonPayload, null);
        }

        if (payload is null
            || string.IsNullOrWhiteSpace(envelope.AggregateId)
            || string.IsNullOrWhiteSpace(payload.WorkspaceId))
        {
            return Rejection(envelope, FolderResultCode.ValidationFailed, null);
        }

        LayeredFolderAuthorizationAllowedContext? allowed = _authorizationAccessor.Current?.AllowedContext;
        if (allowed is null
            || string.IsNullOrWhiteSpace(allowed.OrganizationId)
            || string.IsNullOrWhiteSpace(allowed.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(allowed.ActorSafeIdentifier))
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        string taskId = ReadRequiredTaskId(envelope);
        Dictionary<string, string?> principalValues = new(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(envelope.UserId))
        {
            principalValues["eventstore_envelope_user"] = envelope.UserId;
        }

        WorkspacePreparationRequest request = new(
            allowed.AuthoritativeTenantId,
            allowed.ActorSafeIdentifier,
            EventStoreClaimTransformEvidence.Allowed(
                allowed.AuthoritativeTenantId,
                allowed.ActorSafeIdentifier,
                [
                    WorkspacePreparationService.ActionToken,
                    ProviderReadinessValidationService.ReadActionToken,
                ]),
            envelope.AggregateId,
            payload.RequestSchemaVersion ?? string.Empty,
            payload.WorkspaceId ?? string.Empty,
            payload.RepositoryBindingId ?? string.Empty,
            payload.BranchRefPolicyRef ?? string.Empty,
            payload.WorkspacePolicyRef ?? string.Empty,
            envelope.CorrelationId,
            taskId,
            envelope.MessageId,
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["eventstore_envelope_tenant"] = envelope.TenantId,
            },
            ClientControlledPrincipalValues: principalValues);

        FolderResult result;
        try
        {
            result = await _workspacePreparationService.PrepareAsync(request, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        return ToDomainResult(envelope, result);
    }

    private async Task<DomainResult> ProcessLockWorkspaceAsync(CommandEnvelope envelope)
    {
        LockWorkspacePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<LockWorkspacePayload>(envelope.Payload, PayloadJsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Rejection(envelope, FolderResultCode.MalformedJsonPayload, null);
        }

        if (payload is null
            || string.IsNullOrWhiteSpace(envelope.AggregateId)
            || string.IsNullOrWhiteSpace(payload.WorkspaceId))
        {
            return Rejection(envelope, FolderResultCode.ValidationFailed, null);
        }

        LayeredFolderAuthorizationAllowedContext? allowed = _authorizationAccessor.Current?.AllowedContext;
        if (allowed is null
            || string.IsNullOrWhiteSpace(allowed.OrganizationId)
            || string.IsNullOrWhiteSpace(allowed.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(allowed.ActorSafeIdentifier))
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        string taskId = ReadRequiredTaskId(envelope);
        Dictionary<string, string?> principalValues = new(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(envelope.UserId))
        {
            principalValues["eventstore_envelope_user"] = envelope.UserId;
        }

        WorkspaceLockAcquisitionRequest request = new(
            allowed.AuthoritativeTenantId,
            allowed.ActorSafeIdentifier,
            EventStoreClaimTransformEvidence.Allowed(
                allowed.AuthoritativeTenantId,
                allowed.ActorSafeIdentifier,
                [WorkspaceLockAcquisitionService.ActionToken]),
            envelope.AggregateId,
            payload.RequestSchemaVersion ?? string.Empty,
            payload.WorkspaceId ?? string.Empty,
            payload.LockIntent ?? string.Empty,
            payload.RequestedLeaseSeconds ?? 0,
            envelope.CorrelationId,
            taskId,
            envelope.MessageId,
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["eventstore_envelope_tenant"] = envelope.TenantId,
            },
            ClientControlledPrincipalValues: principalValues);

        FolderResult result;
        try
        {
            result = await _workspaceLockAcquisitionService.AcquireAsync(request, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        return ToDomainResult(envelope, result);
    }

    private async Task<DomainResult> ProcessReleaseWorkspaceLockAsync(CommandEnvelope envelope)
    {
        ReleaseWorkspaceLockPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ReleaseWorkspaceLockPayload>(envelope.Payload, PayloadJsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Rejection(envelope, FolderResultCode.MalformedJsonPayload, null);
        }

        if (payload is null
            || string.IsNullOrWhiteSpace(envelope.AggregateId)
            || string.IsNullOrWhiteSpace(payload.WorkspaceId)
            || string.IsNullOrWhiteSpace(payload.LockId)
            || string.IsNullOrWhiteSpace(payload.LockOwnershipProof))
        {
            return Rejection(envelope, FolderResultCode.ValidationFailed, null);
        }

        LayeredFolderAuthorizationAllowedContext? allowed = _authorizationAccessor.Current?.AllowedContext;
        if (allowed is null
            || string.IsNullOrWhiteSpace(allowed.OrganizationId)
            || string.IsNullOrWhiteSpace(allowed.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(allowed.ActorSafeIdentifier))
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        string taskId = ReadRequiredTaskId(envelope);
        Dictionary<string, string?> principalValues = new(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(envelope.UserId))
        {
            principalValues["eventstore_envelope_user"] = envelope.UserId;
        }

        WorkspaceLockReleaseRequest request = new(
            allowed.AuthoritativeTenantId,
            allowed.ActorSafeIdentifier,
            EventStoreClaimTransformEvidence.Allowed(
                allowed.AuthoritativeTenantId,
                allowed.ActorSafeIdentifier,
                [WorkspaceLockReleaseService.ActionToken]),
            envelope.AggregateId,
            payload.RequestSchemaVersion ?? string.Empty,
            payload.WorkspaceId ?? string.Empty,
            payload.LockId ?? string.Empty,
            payload.LockOwnershipProof ?? string.Empty,
            payload.ReleaseReasonCode ?? string.Empty,
            envelope.CorrelationId,
            taskId,
            envelope.MessageId,
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["eventstore_envelope_tenant"] = envelope.TenantId,
            },
            ClientControlledPrincipalValues: principalValues);

        FolderResult result;
        try
        {
            result = await _workspaceLockReleaseService.ReleaseAsync(request, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        return ToDomainResult(envelope, result);
    }

    private async Task<DomainResult> ProcessMutateFilesAsync(CommandEnvelope envelope)
    {
        FileMutationPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<FileMutationPayload>(envelope.Payload, PayloadJsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Rejection(envelope, FolderResultCode.MalformedJsonPayload, null);
        }

        if (payload is null
            || payload.PathMetadata is null
            || string.IsNullOrWhiteSpace(envelope.AggregateId)
            || string.IsNullOrWhiteSpace(payload.WorkspaceId))
        {
            return Rejection(envelope, FolderResultCode.ValidationFailed, null);
        }

        LayeredFolderAuthorizationAllowedContext? allowed = _authorizationAccessor.Current?.AllowedContext;
        if (allowed is null
            || string.IsNullOrWhiteSpace(allowed.OrganizationId)
            || string.IsNullOrWhiteSpace(allowed.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(allowed.ActorSafeIdentifier))
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        string taskId = ReadRequiredTaskId(envelope);
        Dictionary<string, string?> principalValues = new(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(envelope.UserId))
        {
            principalValues["eventstore_envelope_user"] = envelope.UserId;
        }

        WorkspaceFileMutationRequest request = new(
            allowed.AuthoritativeTenantId,
            allowed.ActorSafeIdentifier,
            EventStoreClaimTransformEvidence.Allowed(
                allowed.AuthoritativeTenantId,
                allowed.ActorSafeIdentifier,
                [WorkspaceFileMutationService.ActionToken]),
            envelope.AggregateId,
            payload.RequestSchemaVersion ?? string.Empty,
            payload.WorkspaceId ?? string.Empty,
            payload.OperationId ?? string.Empty,
            payload.FileOperationKind ?? string.Empty,
            payload.TransportOperation ?? string.Empty,
            new PathMetadata(
                payload.PathMetadata.NormalizedPath ?? string.Empty,
                payload.PathMetadata.DisplayName ?? string.Empty,
                payload.PathMetadata.PathPolicyClass ?? string.Empty,
                payload.PathMetadata.UnicodeNormalization ?? string.Empty),
            payload.ContentHashReference,
            payload.ByteLength,
            payload.MediaType,
            payload.TransportEvidenceKind,
            payload.ObservedByteLength,
            envelope.CorrelationId,
            taskId,
            envelope.MessageId,
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["eventstore_envelope_tenant"] = envelope.TenantId,
            },
            ClientControlledPrincipalValues: principalValues);

        FolderResult result;
        try
        {
            result = await _workspaceFileMutationService.MutateAsync(request, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        return ToDomainResult(envelope, result);
    }

    private async Task<DomainResult> ProcessCommitWorkspaceAsync(CommandEnvelope envelope)
    {
        CommitWorkspacePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<CommitWorkspacePayload>(envelope.Payload, PayloadJsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Rejection(envelope, FolderResultCode.MalformedJsonPayload, null);
        }

        if (payload is null
            || string.IsNullOrWhiteSpace(envelope.AggregateId)
            || string.IsNullOrWhiteSpace(payload.WorkspaceId))
        {
            return Rejection(envelope, FolderResultCode.ValidationFailed, null);
        }

        LayeredFolderAuthorizationAllowedContext? allowed = _authorizationAccessor.Current?.AllowedContext;
        if (allowed is null
            || string.IsNullOrWhiteSpace(allowed.OrganizationId)
            || string.IsNullOrWhiteSpace(allowed.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(allowed.ActorSafeIdentifier))
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        string taskId = ReadRequiredTaskId(envelope);
        if (!string.Equals(payload.TaskId, taskId, StringComparison.Ordinal))
        {
            return Rejection(envelope, FolderResultCode.ValidationFailed, null);
        }

        Dictionary<string, string?> principalValues = new(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(envelope.UserId))
        {
            principalValues["eventstore_envelope_user"] = envelope.UserId;
        }

        WorkspaceCommitRequest request = new(
            allowed.AuthoritativeTenantId,
            allowed.ActorSafeIdentifier,
            EventStoreClaimTransformEvidence.Allowed(
                allowed.AuthoritativeTenantId,
                allowed.ActorSafeIdentifier,
                [WorkspaceCommitService.ActionToken]),
            envelope.AggregateId,
            payload.RequestSchemaVersion ?? string.Empty,
            payload.WorkspaceId ?? string.Empty,
            payload.OperationId ?? string.Empty,
            payload.AuthorMetadataReference ?? string.Empty,
            payload.BranchRefTarget ?? string.Empty,
            payload.CommitMessageClassification ?? string.Empty,
            payload.ChangedPathMetadataDigest ?? string.Empty,
            envelope.CorrelationId,
            taskId,
            envelope.MessageId,
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["eventstore_envelope_tenant"] = envelope.TenantId,
            },
            ClientControlledPrincipalValues: principalValues);

        FolderResult result;
        try
        {
            result = await _workspaceCommitService.CommitAsync(request, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        return ToDomainResult(envelope, result);
    }

    private static string ReadRequiredTaskId(CommandEnvelope envelope)
        => TryReadCanonicalExtension(envelope.Extensions, "taskId")!;

    private static bool IsValidProcessEnvelope(CommandEnvelope envelope)
        => IsCanonicalIdentifier(envelope.MessageId)
        && IsCanonicalIdentifier(envelope.CorrelationId)
        && IsCanonicalIdentifier(envelope.TenantId)
        && IsCanonicalIdentifier(envelope.AggregateId)
        && IsCanonicalIdentifier(envelope.UserId)
        && TryReadCanonicalExtension(envelope.Extensions, "taskId") is not null;

    private static string? TryReadCanonicalExtension(IReadOnlyDictionary<string, string>? extensions, string key)
    {
        if (extensions is null
            || !extensions.TryGetValue(key, out string? value)
            || string.IsNullOrWhiteSpace(value)
            || value.Length > FoldersServerModule.MaxCanonicalIdentifierLength
            || !CanonicalSegmentRegex().IsMatch(value))
        {
            return null;
        }

        return value;
    }

    private static bool IsCanonicalIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= FoldersServerModule.MaxCanonicalIdentifierLength
        && CanonicalSegmentRegex().IsMatch(value);

    // Caller (ProcessArchiveAsync) already guards against null `allowed` before calling this
    // method, so the parameter is non-nullable. The dead null arm was removed to avoid
    // misleading readers into thinking the path can fire.
    private static TenantAccessAuthorizationResult BuildTenantAccess(LayeredFolderAuthorizationAllowedContext allowed)
    {
        ArgumentNullException.ThrowIfNull(allowed);
        return new(
            TenantAccessOutcome.Allowed,
            "allowed",
            allowed.AuthoritativeTenantId,
            allowed.FreshnessWatermark,
            null,
            null,
            MapFreshness(allowed),
            "layered-authorization");
    }

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
                or FolderResultCode.AlreadyApplied => AcceptedNoOp(result),
            _ => Rejection(envelope, result.Code, result),
        };

    private static DomainResult AcceptedNoOp(FolderResult result)
    {
        FolderProcessResultPayload payload = new(
            Status: "accepted",
            IdempotentReplay: result.Code == FolderResultCode.IdempotentReplay,
            CorrelationId: IsCanonicalIdentifier(result.CorrelationId) ? result.CorrelationId : null,
            TaskId: IsCanonicalIdentifier(result.TaskId) ? result.TaskId : null,
            IdempotencyKey: IsCanonicalIdentifier(result.IdempotencyKey) ? result.IdempotencyKey : null);

        return new PayloadNoOpDomainResult(JsonSerializer.Serialize(payload, PayloadJsonOptions));
    }

    private static DomainResult Rejection(CommandEnvelope envelope, FolderResultCode code, FolderResult? result)
    {
        // FolderCommandRejected.Create canonicalizes correlation/idempotency identifiers
        // before they enter the rejection event so that an internal /process caller cannot
        // smuggle CR/LF or oversized values into downstream log/trace surfaces. Values that
        // fail the canonical filter are dropped to null and a metadata-only trace tag is
        // stamped so operators can correlate the dropped-value case without identifier text
        // leaking through the payload.
        IRejectionEvent rejection = CreateRejectionEvent(envelope, code, result);

        return DomainResult.Rejection([rejection]);
    }

    private static IRejectionEvent CreateRejectionEvent(CommandEnvelope envelope, FolderResultCode code, FolderResult? result)
    {
        string correlationId = result?.CorrelationId ?? envelope.CorrelationId;
        string? taskId = result?.TaskId ?? TryReadCanonicalExtension(envelope.Extensions, "taskId");
        string idempotencyKey = result?.IdempotencyKey ?? envelope.MessageId;

        if (code == FolderResultCode.LockConflict
            && string.Equals(envelope.CommandType, FoldersServerModule.LockWorkspaceCommandType, StringComparison.Ordinal))
        {
            return DuplicateWorkspaceLockRejected.Create(envelope.CommandType, correlationId, taskId, idempotencyKey);
        }

        if ((code is FolderResultCode.LockNotOwned or FolderResultCode.LockExpired)
            && string.Equals(envelope.CommandType, FoldersServerModule.ReleaseWorkspaceLockCommandType, StringComparison.Ordinal))
        {
            return FolderCommandRejected.Create(
                code: code.ToString(),
                commandType: envelope.CommandType,
                managedTenantId: result?.ManagedTenantId,
                organizationId: result?.OrganizationId,
                folderId: result?.FolderId,
                actorPrincipalId: result?.ActorPrincipalId,
                correlationId: correlationId,
                taskId: taskId,
                idempotencyKey: idempotencyKey);
        }

        if (code == FolderResultCode.StateTransitionInvalid
            && (string.Equals(envelope.CommandType, FoldersServerModule.PrepareWorkspaceCommandType, StringComparison.Ordinal)
                || string.Equals(envelope.CommandType, FoldersServerModule.LockWorkspaceCommandType, StringComparison.Ordinal)
                || string.Equals(envelope.CommandType, FoldersServerModule.ReleaseWorkspaceLockCommandType, StringComparison.Ordinal)
                || string.Equals(envelope.CommandType, FoldersServerModule.CommitWorkspaceCommandType, StringComparison.Ordinal)))
        {
            return WorkspaceTransitionInvalidRejected.Create(envelope.CommandType, correlationId, taskId, idempotencyKey);
        }

        return FolderCommandRejected.Create(
            code: code.ToString(),
            commandType: envelope.CommandType,
            managedTenantId: result?.ManagedTenantId,
            organizationId: result?.OrganizationId,
            folderId: result?.FolderId,
            actorPrincipalId: result?.ActorPrincipalId,
            correlationId: correlationId,
            taskId: taskId,
            idempotencyKey: idempotencyKey);
    }

    private sealed record PayloadNoOpDomainResult(string Payload) : DomainResult([])
    {
        public override string? ResultPayload => Payload;
    }

    private sealed record FolderProcessResultPayload(
        string Status,
        bool IdempotentReplay,
        string? CorrelationId,
        string? TaskId,
        string? IdempotencyKey);

    private sealed record ArchiveFolderPayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? ArchiveReasonCode);

    private sealed record CreateRepositoryBackedFolderPayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? FolderId,
        [property: JsonRequired] string? RepositoryBindingId,
        [property: JsonRequired] string? ProviderBindingRef,
        [property: JsonRequired] string? RepositoryProfileRef,
        [property: JsonRequired] FolderMetadataPayload? FolderMetadata,
        [property: JsonRequired] BranchRefPolicyPayload? BranchRefPolicy,
        [property: JsonRequired] string? CredentialScopeClass);

    private sealed record FolderMetadataPayload(
        [property: JsonRequired] string? DisplayName,
        [property: JsonRequired] string? MetadataClass);

    private sealed record BranchRefPolicyPayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? RepositoryBindingId,
        [property: JsonRequired] string? PolicyRef,
        string? DefaultRef,
        IReadOnlyList<string>? AllowedRefPatterns,
        IReadOnlyList<string>? ProtectedRefPatterns);

    private sealed record BindRepositoryPayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? ProviderBindingRef,
        [property: JsonRequired] string? ExternalRepositoryRef,
        [property: JsonRequired] BranchRefPolicyPayload? BranchRefPolicy);

    private sealed record PrepareWorkspacePayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? WorkspaceId,
        [property: JsonRequired] string? RepositoryBindingId,
        [property: JsonRequired] string? BranchRefPolicyRef,
        [property: JsonRequired] string? WorkspacePolicyRef);

    private sealed record LockWorkspacePayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? WorkspaceId,
        [property: JsonRequired] string? LockIntent,
        [property: JsonRequired] int? RequestedLeaseSeconds);

    private sealed record ReleaseWorkspaceLockPayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? WorkspaceId,
        [property: JsonRequired] string? LockId,
        [property: JsonRequired] string? LockOwnershipProof,
        [property: JsonRequired] string? ReleaseReasonCode);

    private sealed record PathMetadataPayload(
        [property: JsonRequired] string? NormalizedPath,
        [property: JsonRequired] string? DisplayName,
        [property: JsonRequired] string? PathPolicyClass,
        [property: JsonRequired] string? UnicodeNormalization);

    private sealed record FileMutationPayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? WorkspaceId,
        [property: JsonRequired] string? OperationId,
        [property: JsonRequired] string? FileOperationKind,
        [property: JsonRequired] string? TransportOperation,
        [property: JsonRequired] PathMetadataPayload? PathMetadata,
        string? ContentHashReference,
        long? ByteLength,
        string? MediaType,
        string? TransportEvidenceKind,
        long? ObservedByteLength);

    private sealed record CommitWorkspacePayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? WorkspaceId,
        [property: JsonRequired] string? OperationId,
        [property: JsonRequired] string? TaskId,
        [property: JsonRequired] string? BranchRefTarget,
        [property: JsonRequired] string? ChangedPathMetadataDigest,
        [property: JsonRequired] string? AuthorMetadataReference,
        [property: JsonRequired] string? CommitMessageClassification,
        [property: JsonRequired] IReadOnlyList<string>? AuditMetadataKeys);
}
