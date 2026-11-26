using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authorization;
using Hexalith.Folders.Server.Content;

namespace Hexalith.Folders.Server.Provisioning;

/// <summary>Executes the restricted Folder command and resolves its receipt from the committed Folder stream.</summary>
internal sealed class FolderProvisioningService(
    DurableTaskStepLookupClient steps,
    TenantAccessAuthorizer tenantAccess,
    EventStoreOrganizationEffectivePermissionsReadModel permissions,
    IAuthoritativeEventStreamReader source,
    FolderEventReplay replay,
    IConfiguration configuration,
    TimeProvider clock,
    IHttpContextAccessor http,
    FolderProvisioningStreamPresence? streamPresence = null)
{
    internal const string Section = DurableTaskStepLookupClient.Section;
    internal const string ReadAction = "read_metadata";
    internal const string ManageAction = "manage_folder_access";
    private const long ProvisionedVersion = 3;

    internal static JsonSerializerOptions WireJson { get; } = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    internal static bool MutationsFrozen(IConfiguration? configuration)
        => configuration?.GetValue<bool>(Section + "FreezeMutations") == true;

    internal async Task<IResult> ProcessAsync(DomainServiceRequest request, CancellationToken token)
    {
        IResult result = await DecideAsync(request, token).ConfigureAwait(false);
        return result is IStatusCodeHttpResult status
            && status.StatusCode is StatusCodes.Status404NotFound or StatusCodes.Status503ServiceUnavailable
                ? new AuthorizationDenialTransportResult(result)
                : result;
    }

    private async Task<IResult> DecideAsync(DomainServiceRequest request, CancellationToken token)
    {
        CommandEnvelope command = request.Command;
        string? worker = configuration[Section + "WorkerWorkload"];
        ClaimsPrincipal? callback = http.HttpContext?.User;
        if (string.IsNullOrWhiteSpace(worker) || command.Domain != FoldersServerModule.DomainName
            || command.Extensions?.GetValueOrDefault(EventStoreGatewayVerifiedOrigin.ExtensionKey) != worker
            || callback?.Identity?.IsAuthenticated != true
            || !callback.HasClaim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType,
                configuration["Authentication:HumanAuthorityReceipt:ExpectedWorkload"] ?? "eventstore")
            || !callback.HasClaim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType,
                EventStoreWorkloadOperations.DomainServiceProcess))
        {
            return Results.NotFound();
        }

        if (!configuration.GetValue<bool>(Section + "Enabled") || MutationsFrozen(configuration))
        {
            return Unavailable();
        }

        try
        {
            FolderProvisioningRequest? intent = FolderDomainProcessor.ReadProvisioningCommand(command);
            if (!TryScope(command.TenantId, intent, out string folderId) || command.AggregateId != folderId)
            {
                return Results.NotFound();
            }

            CommittedProvisioningStep? binding = await steps.LookupAsync(command.TenantId, folderId, intent!, token)
                .ConfigureAwait(false);
            if (binding is null)
            {
                return Results.NotFound();
            }

            TenantAccessAuthorizationResult member = await GetCurrentMemberAsync(binding, token).ConfigureAwait(false);
            if (MembershipFailure(member) is { } denied)
            {
                return denied;
            }

            if (!MatchesDigest(intent!) || intent!.ExpectedVersion != 0)
            {
                return CommandDecision("conflict");
            }

            DomainServiceCurrentState? actorState = DomainStateReplay.SnapshotAware(request.CurrentState);
            long head = actorState?.CurrentSequence ?? 0;
            if (head > 0)
            {
                FolderState state = replay.FoldFolder(request.CurrentState, binding.TenantId, folderId);
                if (state.ProvisioningReceipt is null)
                {
                    IResult? deniedCollision = await CheckCreateAuthorityAsync(binding, token).ConfigureAwait(false);
                    return deniedCollision ?? CommandDecision("conflict");
                }

                CommittedProvisioningStep? refreshed = await steps.LookupAsync(binding.TenantId, folderId, intent, token)
                    .ConfigureAwait(false);
                if (refreshed is null || refreshed.OriginalActorId != binding.OriginalActorId)
                {
                    return Results.NotFound();
                }

                // Gateway result retention cannot substitute for disclosure-time authorization.
                IResult receipt = ResolveReceipt(state, head, refreshed, member);
                return receipt is IStatusCodeHttpResult status
                    ? status.StatusCode switch
                    {
                        StatusCodes.Status200OK => CommandDecision("receipt_available"),
                        StatusCodes.Status409Conflict => CommandDecision("conflict"),
                        _ => receipt,
                    }
                    : Unavailable();
            }

            if (head < 0 || actorState?.SnapshotState is not null || actorState?.EventCount > 0
                || (actorState?.LastSnapshotSequence ?? 0) != 0
                || actorState is null && request.CurrentState is not (null or JsonElement { ValueKind: JsonValueKind.Null }))
            {
                return Unavailable();
            }

            if (binding.Receipt is not null)
            {
                return Unavailable();
            }

            if (!binding.AcceptsExecution)
            {
                return Results.NotFound();
            }

            IResult? authorizationFailure = await CheckCreateAuthorityAsync(binding, token).ConfigureAwait(false);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            member = await GetCurrentMemberAsync(binding, token).ConfigureAwait(false);
            if (MembershipFailure(member) is { } finalDenial)
            {
                return finalDenial;
            }

            authorizationFailure = await CheckCreateAuthorityAsync(binding, token).ConfigureAwait(false);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            CommittedProvisioningStep? final = await steps.LookupAsync(binding.TenantId, folderId, intent, token)
                .ConfigureAwait(false);
            if (final is null || final != binding || !final.AcceptsExecution)
            {
                return Results.NotFound();
            }

            if (!configuration.GetValue<bool>(Section + "Enabled") || MutationsFrozen(configuration))
            {
                return Unavailable();
            }

            return Create(intent, binding, folderId, command.CorrelationId);
        }
        catch (JsonException)
        {
            return Unavailable();
        }
        catch (Exception exception) when (IsDependencyFailure(exception, token))
        {
            return Unavailable();
        }
    }

    internal async Task<IResult> LookupAsync(string tenantId, FolderProvisioningRequest request, CancellationToken token)
    {
        if (!TryScope(tenantId, request, out string folderId))
        {
            return Results.NotFound();
        }

        try
        {
            CommittedProvisioningStep? binding = await steps.LookupAsync(tenantId, folderId, request, token).ConfigureAwait(false);
            if (binding is null)
            {
                return Results.NotFound();
            }

            TenantAccessAuthorizationResult member = await GetCurrentMemberAsync(binding, token).ConfigureAwait(false);
            if (MembershipFailure(member) is { } denied)
            {
                return denied;
            }

            if (!MatchesDigest(request) || request.ExpectedVersion != 0)
            {
                return Conflict();
            }

            var identity = new AggregateIdentity(tenantId, FoldersServerModule.DomainName, folderId);
            var observation = await source.ReadAsync(identity, token).ConfigureAwait(false);
            FolderState? state = null;
            long head = 0;
            if (!observation.IsAuthoritative || observation.Stream is null)
            {
                // An absent actor has no native proof. A read outage must stay distinct
                // from an authenticated missing-stream decision before a worker executes.
                if (binding.Receipt is not null || !binding.AcceptsExecution || streamPresence is null
                    || !await streamPresence.IsMissingAsync(identity, token).ConfigureAwait(false))
                {
                    return Unavailable();
                }
            }
            else
            {
                var current = await source.ReadAsync(identity, token).ConfigureAwait(false);
                if (!current.IsAuthoritative || current.Stream is not { } prefix || prefix.Head != observation.Stream.Head
                    || prefix.Identity != identity || observation.Stream.Identity != identity || prefix.NativeProof != observation.Stream.NativeProof)
                {
                    return Unavailable();
                }

                head = prefix.Head;
                if (head > 0)
                {
                    state = WorkspaceContentStateReader.FoldCommittedStream(replay, prefix, tenantId, identity.Domain, folderId);
                }
            }

            if (state?.ProvisioningReceipt is null)
            {
                IResult? deniedCreate = await CheckCreateAuthorityAsync(binding, token).ConfigureAwait(false);
                if (deniedCreate is not null)
                {
                    return deniedCreate;
                }
            }

            member = await GetCurrentMemberAsync(binding, token).ConfigureAwait(false);
            if (MembershipFailure(member) is { } finalDenial)
            {
                return finalDenial;
            }

            CommittedProvisioningStep? final = await steps.LookupAsync(tenantId, folderId, request, token).ConfigureAwait(false);
            if (final is null || final.OriginalActorId != binding.OriginalActorId)
            {
                return Results.NotFound();
            }

            if (state is not null)
            {
                return ResolveReceipt(state, head, final, member);
            }

            if (final.Receipt is not null)
            {
                return Unavailable();
            }

            return final.AcceptsExecution ? Results.NoContent() : Results.NotFound();
        }
        catch (Exception exception) when (IsDependencyFailure(exception, token))
        {
            return Unavailable();
        }
    }

    private async Task<IResult?> CheckCreateAuthorityAsync(CommittedProvisioningStep binding, CancellationToken token)
    {
        var actor = EffectivePermissionPrincipal.User(binding.OriginalActorId);
        const string baseline = FolderStreamName.ReservedOrganizationPermissionScope;
        EffectivePermissionsReadModelResult read = await permissions.GetAsync(new(
            binding.TenantId, baseline, [actor], binding.TaskId, null,
            "read_your_writes", WaitForCatchUp: true), token).ConfigureAwait(false);
        if (read.Status == EffectivePermissionsReadModelStatus.NotFound)
        {
            return Results.NotFound();
        }

        if (read.Status != EffectivePermissionsReadModelStatus.Available || read.Snapshot is not { } snapshot
            || snapshot.ManagedTenantId != binding.TenantId || snapshot.OrganizationId != binding.TenantId
            || snapshot.FolderId != baseline || !snapshot.RevocationFreshnessEstablished || snapshot.Freshness.Stale
            || string.IsNullOrWhiteSpace(snapshot.PermissionVersion))
        {
            return Unavailable();
        }

        var actions = snapshot.EvidenceRows
            .Where(row => row.Principal == actor && row.Source == EffectivePermissionEvidenceSource.OrganizationBaselineGrant)
            .Select(row => row.Action)
            .ToHashSet(StringComparer.Ordinal);
        return actions.Contains(FolderCreationService.ActionToken) && actions.Contains(ReadAction) && actions.Contains(ManageAction)
            ? null
            : Results.NotFound();
    }

    private static bool TryScope(string tenantId, FolderProvisioningRequest? request, out string folderId)
    {
        folderId = string.Empty;
        if (!FolderCanonicalSegmentIdentifier.IsLowercaseSegment(tenantId) || tenantId == "system"
            || request is null || request.Fence <= 0
            || !FolderProvisioningContract.IsCanonicalStep(request.TaskId, request.StepKey)
            || request.AdmissionOperation is not { Length: > 0 and <= 128 }
            || request.DisplayName is null || request.DisplayName.AsSpan().Trim().Length is < 1 or > FolderCommandValidator.MaxProvisioningDisplayNameLength
            || request.RequestSha256 is not { Length: 64 }
            || request.RequestSha256.Any(character => character is not (>= 'a' and <= 'f' or >= '0' and <= '9')))
        {
            return false;
        }

        folderId = FolderProvisioningContract.DeriveFolderId(tenantId, request.StepKey);
        return FolderStreamName.TryCreate(tenantId, folderId, out _, out _);
    }

    private static bool MatchesDigest(FolderProvisioningRequest request)
        => FolderProvisioningContract.ComputeRequestSha256(request.DisplayName) == request.RequestSha256;

    private static IResult? MembershipFailure(TenantAccessAuthorizationResult member)
        => member.Outcome switch
        {
            TenantAccessOutcome.Allowed when member.FreshnessStatus == TenantProjectionFreshnessStatus.Fresh
                && !string.IsNullOrWhiteSpace(member.ProjectionWatermark) => null,
            TenantAccessOutcome.Denied or TenantAccessOutcome.UnknownTenant or TenantAccessOutcome.DisabledTenant => Results.NotFound(),
            _ => Unavailable(),
        };

    private async Task<TenantAccessAuthorizationResult> GetCurrentMemberAsync(CommittedProvisioningStep binding, CancellationToken token)
    {
        using IDisposable suppression = PreauthorizedRequestContext.SuppressReuse();
        return await tenantAccess.AuthorizeMutationAsync(new(binding.TenantId, binding.OriginalActorId, binding.TenantId), token)
            .ConfigureAwait(false);
    }

    private IResult Create(FolderProvisioningRequest intent, CommittedProvisioningStep binding, string folderId, string correlation)
    {
        string receiptId = ReceiptId(binding);
        var command = new CreateFolder(binding.TenantId, binding.TenantId, folderId, intent.DisplayName,
            null, null, [], binding.OriginalActorId, correlation, binding.TaskId.ToLowerInvariant(), receiptId, binding.TenantId);
        FolderResult result = FolderAggregate.HandleProvisioning(command, clock.GetUtcNow());
        if (result.Events is not [FolderCreated created])
        {
            return Results.BadRequest();
        }

        var receipt = new FolderProvisioningReceipt(receiptId, binding.TenantId, folderId, binding.OriginalActorId,
            binding.TaskId, binding.StepKey, binding.AdmissionOperation, binding.RequestSha256, ProvisionedVersion);
        return CommandResult(DomainResult.Success(
        [
            created with { ProvisioningReceipt = receipt, InheritOrganizationPermissions = false },
            Grant(ReadAction, 1),
            Grant(ManageAction, 2),
        ]));

        FolderAccessGranted Grant(string action, long accessSequence)
            => new(created.ManagedTenantId, created.OrganizationId, created.FolderId, FolderAccessPrincipalKind.User,
                binding.OriginalActorId, action, binding.OriginalActorId, created.CorrelationId, created.TaskId,
                receiptId + "-" + accessSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
                created.IdempotencyFingerprint, accessSequence, created.OccurredAt);
    }

    private IResult ResolveReceipt(FolderState state, long head, CommittedProvisioningStep binding, TenantAccessAuthorizationResult member)
    {
        string folderId = FolderProvisioningContract.DeriveFolderId(binding.TenantId, binding.StepKey);
        FolderProvisioningReceipt? receipt = state.ProvisioningReceipt;
        if (receipt is null)
        {
            return Conflict();
        }

        if (receipt.AdmissionOperation != binding.AdmissionOperation || receipt.RequestSha256 != binding.RequestSha256)
        {
            return Conflict();
        }

        if (receipt.TenantId != binding.TenantId || receipt.FolderId != folderId || receipt.TaskId != binding.TaskId
            || receipt.StepKey != binding.StepKey || receipt.OriginalActorId != binding.OriginalActorId
            || receipt.ReceiptId != ReceiptId(binding) || receipt.OwnerVersion != ProvisionedVersion
            || state.InheritOrganizationPermissions != false || head < receipt.OwnerVersion
            || binding.Receipt is not null && binding.Receipt != receipt.ReceiptId)
        {
            return Unavailable();
        }

        if (state.LifecycleState != FolderLifecycleState.Active || !Granted(ReadAction) || !Granted(ManageAction))
        {
            return Results.NotFound();
        }

        return Results.Ok(new
        {
            Receipt = receipt,
            CurrentOwnerVersion = head,
            Access = new
            {
                Read = ReadAction,
                Manage = ManageAction,
                TenantRevision = member.ProjectionWatermark,
                VerifiedAtUtc = clock.GetUtcNow(),
            },
            Disposition = "NeedsAttention",
        });

        bool Granted(string action) => state.AccessOverrides.TryGetValue(
            new(binding.TenantId, folderId, FolderAccessPrincipalKind.User, binding.OriginalActorId, action), out var grant)
                && grant.IsGranted;
    }

    private static string ReceiptId(CommittedProvisioningStep binding)
        => "prov-" + FolderProvisioningContract.DeriveFolderId(binding.TenantId, binding.StepKey)["fld-p-".Length..];

    private static bool IsDependencyFailure(Exception exception, CancellationToken token)
        => exception is HttpRequestException or EventStoreGatewayException or InvalidOperationException or JsonException
            or TimeoutException or CryptographicException or FormatException or ArgumentException
            || exception is OperationCanceledException && !token.IsCancellationRequested;

    private static IResult CommandResult(DomainResult result) => Results.Ok(DomainServiceWireResult.FromDomainResult(result));

    private static IResult CommandDecision(string status)
        => Results.Ok(new DomainServiceWireResult(false, [], JsonSerializer.Serialize(new { status }, WireJson)));

    private static IResult Conflict() => Results.Conflict();

    private static IResult Unavailable() => Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
}
