using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.Folders;

public sealed class WorkspaceLockStatusQueryHandler(
    LayeredFolderAuthorizationService authorizationService,
    IWorkspaceLockStatusReadModel readModel,
    IUtcClock clock,
    ILogger<WorkspaceLockStatusQueryHandler>? logger = null)
{
    public const string ActionToken = "read_workspace_lock";
    private const string ActorPresentIdentifier = "actor_present";
    private const string DeniedSafeOutcome = "denied_safe";
    private const string ReadYourWrites = "read_your_writes";

    private readonly ILogger<WorkspaceLockStatusQueryHandler> _logger = logger ?? NullLogger<WorkspaceLockStatusQueryHandler>.Instance;

    public async Task<WorkspaceLockStatusQueryResult> HandleAsync(
        WorkspaceLockStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        FolderLifecycleFreshness deniedFreshness = new(ReadYourWrites, clock.UtcNow, null, Stale: true, "denied_safe");
        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(query.PrincipalId))
        {
            return SafeResult(WorkspaceLockStatusQueryResultCode.AuthenticationRequired, deniedFreshness, query, null);
        }

        if (string.IsNullOrWhiteSpace(query.FolderId) || string.IsNullOrWhiteSpace(query.WorkspaceId))
        {
            return SafeResult(WorkspaceLockStatusQueryResultCode.NotFoundSafe, deniedFreshness, query, null);
        }

        LayeredFolderAuthorizationResult authorization = await authorizationService.AuthorizeAsync(
            new LayeredFolderAuthorizationContext(
                query.AuthoritativeTenantId,
                query.PrincipalId,
                ActorSafeIdentifier: ActorPresentIdentifier,
                ActionToken,
                LayeredFolderOperationPolicy.StrictRead(),
                query.ClaimTransformEvidence,
                OperationScope: query.FolderId,
                query.CorrelationId,
                query.TaskId,
                query.ClientControlledTenantValues,
                query.ClientControlledPrincipalValues),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAllowed || authorization.AllowedContext is null)
        {
            return SafeResult(MapAuthorizationDenial(authorization), deniedFreshness, query, authorization);
        }

        LayeredFolderAuthorizationAllowedContext allowed = authorization.AllowedContext;
        WorkspaceLockStatusReadModelResult readModelResult;
        try
        {
            readModelResult = await readModel.GetAsync(
                new WorkspaceLockStatusReadModelRequest(
                    allowed.AuthoritativeTenantId,
                    query.FolderId,
                    query.WorkspaceId,
                    query.PrincipalId,
                    ActionToken,
                    query.TaskId,
                    query.CorrelationId,
                    allowed.FreshnessWatermark,
                    ReadYourWrites),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Workspace lock read-model call failed; returning ReadModelUnavailable. Exception type: {ExceptionType}",
                ex.GetType().FullName);
            return SafeResult(
                WorkspaceLockStatusQueryResultCode.ReadModelUnavailable,
                new FolderLifecycleFreshness(ReadYourWrites, clock.UtcNow, null, Stale: true, "read_model_unavailable"),
                query,
                null);
        }

        return readModelResult.Status switch
        {
            WorkspaceLockStatusReadModelStatus.Available when readModelResult.Snapshot is not null =>
                Compute(query, allowed, readModelResult.Snapshot),
            WorkspaceLockStatusReadModelStatus.Available =>
                SafeResult(WorkspaceLockStatusQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = "projection_malformed" }, query, null),
            WorkspaceLockStatusReadModelStatus.Stale =>
                SafeResult(WorkspaceLockStatusQueryResultCode.ProjectionStale, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_stale" }, query, null),
            WorkspaceLockStatusReadModelStatus.Unavailable =>
                SafeResult(WorkspaceLockStatusQueryResultCode.ProjectionUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_unavailable" }, query, null),
            WorkspaceLockStatusReadModelStatus.Malformed =>
                SafeResult(WorkspaceLockStatusQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_malformed" }, query, null),
            WorkspaceLockStatusReadModelStatus.NotFound =>
                SafeResult(WorkspaceLockStatusQueryResultCode.NotFoundSafe, readModelResult.Freshness, query, null),
            _ => SafeResult(WorkspaceLockStatusQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true }, query, null),
        };
    }

    private WorkspaceLockStatusQueryResult Compute(
        WorkspaceLockStatusQuery query,
        LayeredFolderAuthorizationAllowedContext allowed,
        WorkspaceLockStatusReadModelSnapshot snapshot)
    {
        WorkspaceLockStatusQueryResult? incompatible = ValidateSnapshotCompatibility(query, allowed, snapshot);
        if (incompatible is not null)
        {
            return incompatible;
        }

        FolderLifecycleFreshness freshness = snapshot.Freshness.ReadConsistency == ReadYourWrites
            ? snapshot.Freshness
            : snapshot.Freshness with { ReadConsistency = ReadYourWrites };

        bool hasLease = !string.IsNullOrWhiteSpace(snapshot.LockId)
            && snapshot.AcquiredAt is not null
            && snapshot.EffectiveAt is not null
            && snapshot.ExpiresAt is not null;
        bool expired = hasLease && snapshot.ExpiresAt!.Value <= clock.UtcNow && snapshot.LockState == "locked";
        string lockState = expired ? "expired" : snapshot.LockState;
        string leaseStatus = expired ? "expired" : lockState == "locked" ? "active" : "released";
        WorkspaceLockLeaseMetadata? lease = hasLease
            ? new(
                snapshot.LockId!,
                leaseStatus,
                snapshot.AcquiredAt!.Value,
                snapshot.EffectiveAt!.Value,
                snapshot.ExpiresAt!.Value,
                lockState == "locked" ? snapshot.HolderTaskId : null)
            : null;

        WorkspaceLockRetryEligibility retry = lockState switch
        {
            "locked" => new(false, null, "lock_active", snapshot.CorrelationId ?? query.CorrelationId, snapshot.TaskId, snapshot.WorkspaceState, freshness),
            "expired" => new(true, 0, "lock_conflict_retry", snapshot.CorrelationId ?? query.CorrelationId, snapshot.TaskId, "ready", freshness),
            _ => new(true, null, "retry_not_required", snapshot.CorrelationId ?? query.CorrelationId, null, snapshot.WorkspaceState, freshness),
        };

        return new(
            WorkspaceLockStatusQueryResultCode.Allowed,
            snapshot.WorkspaceId,
            lockState,
            lease,
            retry,
            freshness,
            query.CorrelationId,
            query.TaskId,
            AuthorizationDenial: null);
    }

    private WorkspaceLockStatusQueryResult? ValidateSnapshotCompatibility(
        WorkspaceLockStatusQuery query,
        LayeredFolderAuthorizationAllowedContext allowed,
        WorkspaceLockStatusReadModelSnapshot snapshot)
    {
        if (snapshot.Freshness.ObservedAt > clock.UtcNow)
        {
            return Unavailable(query, snapshot.Freshness, "freshness_observed_in_future");
        }

        if (!Matches(snapshot.ManagedTenantId, allowed.AuthoritativeTenantId)
            || !Matches(snapshot.FolderId, query.FolderId)
            || !Matches(snapshot.WorkspaceId, query.WorkspaceId))
        {
            return Unavailable(query, snapshot.Freshness, "snapshot_scope_mismatch");
        }

        FolderLifecycleEvidenceScope scope = snapshot.EvidenceScope;
        if (!HasValue(scope.PrincipalId) || !Matches(scope.PrincipalId, query.PrincipalId))
        {
            return Unavailable(query, snapshot.Freshness, "principal_mismatch");
        }

        if (HasValue(scope.ManagedTenantId) && !Matches(scope.ManagedTenantId, allowed.AuthoritativeTenantId))
        {
            return Unavailable(query, snapshot.Freshness, "evidence_tenant_mismatch");
        }

        if (HasValue(scope.ActionToken) && !Matches(scope.ActionToken, ActionToken))
        {
            return Unavailable(query, snapshot.Freshness, "action_mismatch");
        }

        if (HasValue(scope.TaskId) && !Matches(scope.TaskId, query.TaskId))
        {
            return Unavailable(query, snapshot.Freshness, "task_mismatch");
        }

        if (HasValue(scope.CorrelationId) && !Matches(scope.CorrelationId, query.CorrelationId))
        {
            return Unavailable(query, snapshot.Freshness, "correlation_mismatch");
        }

        if (HasValue(scope.AuthorizationWatermark)
            && !Matches(scope.AuthorizationWatermark, allowed.FreshnessWatermark))
        {
            return Unavailable(query, snapshot.Freshness, "incompatible_authorization_watermark");
        }

        return snapshot.Freshness.Stale
            ? Unavailable(query, snapshot.Freshness, snapshot.Freshness.ReasonCode ?? "projection_stale")
            : null;
    }

    private static WorkspaceLockStatusQueryResult Unavailable(
        WorkspaceLockStatusQuery query,
        FolderLifecycleFreshness freshness,
        string reasonCode)
        => SafeResult(
            WorkspaceLockStatusQueryResultCode.ReadModelUnavailable,
            freshness with { Stale = true, ReasonCode = freshness.ReasonCode ?? reasonCode },
            query,
            null);

    private static WorkspaceLockStatusQueryResult SafeResult(
        WorkspaceLockStatusQueryResultCode code,
        FolderLifecycleFreshness freshness,
        WorkspaceLockStatusQuery query,
        LayeredFolderAuthorizationResult? authorizationDenial)
        => new(
            code,
            WorkspaceId: null,
            LockState: DeniedSafeOutcome,
            Lease: null,
            RetryEligibility: new(false, null, DeniedSafeOutcome, query.CorrelationId, query.TaskId, DeniedSafeOutcome, freshness),
            freshness,
            query.CorrelationId,
            query.TaskId,
            authorizationDenial);

    private static WorkspaceLockStatusQueryResultCode MapAuthorizationDenial(LayeredFolderAuthorizationResult authorization)
        => authorization.Decision.OutcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied => WorkspaceLockStatusQueryResultCode.AuthenticationRequired,
            LayeredAuthorizationOutcomeCodes.SafeNotFound or LayeredAuthorizationOutcomeCodes.FolderAclDenied => WorkspaceLockStatusQueryResultCode.NotFoundSafe,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable
                or LayeredAuthorizationOutcomeCodes.TenantProjectionStale
                or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable
                or LayeredAuthorizationOutcomeCodes.FolderAclStale => WorkspaceLockStatusQueryResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied when authorization.Decision.Retryable => WorkspaceLockStatusQueryResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied
                or LayeredAuthorizationOutcomeCodes.ClaimTransformDenied
                or LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied
                or LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed
                or LayeredAuthorizationOutcomeCodes.TenantAccessDenied => WorkspaceLockStatusQueryResultCode.AuthorizationDenied,
            _ => WorkspaceLockStatusQueryResultCode.ReadModelUnavailable,
        };

    private static bool HasValue(string? value)
        => !string.IsNullOrWhiteSpace(value);

    private static bool Matches(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);
}
