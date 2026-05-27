using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.Folders;

public sealed class TaskStatusQueryHandler(
    TenantAccessAuthorizer tenantAccessAuthorizer,
    ITaskStatusReadModel readModel,
    IUtcClock clock,
    ILogger<TaskStatusQueryHandler>? logger = null)
{
    public const string ActionToken = "read_task_status";
    private const string EventuallyConsistent = "eventually_consistent";
    private const string DeniedSafeOutcome = "denied_safe";
    private static readonly HashSet<string> LifecycleStates = new(StringComparer.Ordinal)
    {
        "requested",
        "preparing",
        "ready",
        "locked",
        "changes_staged",
        "dirty",
        "committed",
        "failed",
        "inaccessible",
        "unknown_provider_outcome",
        "reconciliation_required",
    };

    private static readonly HashSet<string> CanonicalErrorCategories = new(StringComparer.Ordinal)
    {
        "success",
        "authentication_failure",
        "tenant_access_denied",
        "folder_acl_denied",
        "validation_error",
        "idempotency_conflict",
        "provider_readiness_failed",
        "provider_permission_insufficient",
        "provider_unavailable",
        "provider_rate_limited",
        "repository_conflict",
        "duplicate_binding",
        "unsupported_provider_capability",
        "workspace_preparation_failed",
        "workspace_locked",
        "lock_conflict",
        "lock_expired",
        "lock_not_owned",
        "path_validation_failed",
        "file_operation_failed",
        "dirty_workspace",
        "commit_failed",
        "provider_failure_known",
        "unknown_provider_outcome",
        "reconciliation_required",
        "not_found",
        "state_transition_invalid",
        "input_limit_exceeded",
        "response_limit_exceeded",
        "query_timeout",
        "read_model_unavailable",
        "projection_stale",
        "projection_unavailable",
        "range_unsatisfiable",
        "failed_operation",
        "redacted",
        "internal_error",
    };

    private readonly ILogger<TaskStatusQueryHandler> _logger = logger ?? NullLogger<TaskStatusQueryHandler>.Instance;

    public async Task<TaskStatusQueryResult> HandleAsync(
        TaskStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        FolderLifecycleFreshness deniedFreshness = new(EventuallyConsistent, clock.UtcNow, null, Stale: true, "denied_safe");
        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(query.PrincipalId))
        {
            return SafeResult(TaskStatusQueryResultCode.AuthenticationRequired, deniedFreshness, query, null);
        }

        if (HasClientControlledMismatch(query.AuthoritativeTenantId, query.ClientControlledTenantValues))
        {
            return SafeResult(TaskStatusQueryResultCode.AuthorizationDenied, deniedFreshness, query, null);
        }

        if (string.IsNullOrWhiteSpace(query.TaskId)
            || !IsCanonicalIdentifier(query.TaskId)
            || (query.CorrelationId is not null && !IsCanonicalIdentifier(query.CorrelationId)))
        {
            return SafeResult(TaskStatusQueryResultCode.NotFoundSafe, deniedFreshness, query, null);
        }

        if (!query.ClaimTransformEvidence.IsPresent
            || query.ClaimTransformEvidence.Malformed
            || !query.ClaimTransformEvidence.HasPermissionFor(ActionToken))
        {
            return SafeResult(TaskStatusQueryResultCode.AuthorizationDenied, deniedFreshness, query, null);
        }

        TenantAccessAuthorizationResult tenantAccess = await tenantAccessAuthorizer
            .AuthorizeDiagnosticReadAsync(
                new TenantAccessAuthorizationContext(
                    query.AuthoritativeTenantId,
                    query.PrincipalId,
                    RequestedTenantId: null),
                cancellationToken)
            .ConfigureAwait(false);

        if (!tenantAccess.IsAllowed)
        {
            return SafeResult(MapTenantAccess(tenantAccess), deniedFreshness, query, tenantAccess);
        }

        TaskStatusReadModelResult readModelResult;
        try
        {
            readModelResult = await readModel.GetAsync(
                new TaskStatusReadModelRequest(
                    query.AuthoritativeTenantId,
                    query.TaskId,
                    query.PrincipalId,
                    ActionToken,
                    query.CorrelationId,
                    EventuallyConsistent),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Task status read-model call failed; returning ReadModelUnavailable. Exception type: {ExceptionType}",
                ex.GetType().FullName);
            return SafeResult(
                TaskStatusQueryResultCode.ReadModelUnavailable,
                new FolderLifecycleFreshness(EventuallyConsistent, clock.UtcNow, null, Stale: true, "read_model_unavailable"),
                query,
                null);
        }

        return readModelResult.Status switch
        {
            TaskStatusReadModelStatus.Available when readModelResult.Snapshot is not null =>
                Compute(query, tenantAccess, readModelResult.Snapshot),
            TaskStatusReadModelStatus.Available =>
                SafeResult(TaskStatusQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = "projection_malformed" }, query, null),
            TaskStatusReadModelStatus.Stale =>
                SafeResult(TaskStatusQueryResultCode.ProjectionStale, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_stale" }, query, null),
            TaskStatusReadModelStatus.Unavailable =>
                SafeResult(TaskStatusQueryResultCode.ProjectionUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_unavailable" }, query, null),
            TaskStatusReadModelStatus.Malformed =>
                SafeResult(TaskStatusQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = "projection_malformed" }, query, null),
            TaskStatusReadModelStatus.NotFound =>
                SafeResult(TaskStatusQueryResultCode.NotFoundSafe, readModelResult.Freshness, query, null),
            _ => SafeResult(TaskStatusQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true }, query, null),
        };
    }

    private static TaskStatusQueryResult Compute(
        TaskStatusQuery query,
        TenantAccessAuthorizationResult tenantAccess,
        TaskStatusReadModelSnapshot snapshot)
    {
        if (!IsContractShapedSnapshot(snapshot, out string? malformedReason)
            || !Matches(snapshot.ManagedTenantId, tenantAccess.TenantId)
            || !Matches(snapshot.TaskId, query.TaskId)
            || snapshot.Freshness.Stale
            || !Matches(snapshot.EvidenceScope.ManagedTenantId, tenantAccess.TenantId)
            || (HasValue(snapshot.EvidenceScope.ActionToken) && !Matches(snapshot.EvidenceScope.ActionToken, ActionToken))
            || (HasValue(snapshot.EvidenceScope.TaskId) && !Matches(snapshot.EvidenceScope.TaskId, query.TaskId)))
        {
            string reasonCode = !string.IsNullOrWhiteSpace(malformedReason)
                ? malformedReason
                : "snapshot_scope_mismatch";
            FolderLifecycleFreshness freshness = snapshot.Freshness is null
                ? new FolderLifecycleFreshness(EventuallyConsistent, DateTimeOffset.UnixEpoch, null, Stale: true, reasonCode)
                : snapshot.Freshness with { Stale = true, ReasonCode = snapshot.Freshness.ReasonCode ?? reasonCode };

            return SafeResult(
                TaskStatusQueryResultCode.ReadModelUnavailable,
                freshness,
                query,
                null);
        }

        return new(
            TaskStatusQueryResultCode.Allowed,
            snapshot.TaskId,
            snapshot.CurrentState,
            snapshot.TerminalState,
            snapshot.LastOperationId,
            snapshot.LastFailureCategory,
            snapshot.RetryEligibility,
            snapshot.RetryAfter,
            snapshot.Freshness with { ReadConsistency = EventuallyConsistent },
            query.CorrelationId,
            AuthorizationDenial: null);
    }

    private static TaskStatusQueryResult SafeResult(
        TaskStatusQueryResultCode code,
        FolderLifecycleFreshness freshness,
        TaskStatusQuery query,
        TenantAccessAuthorizationResult? authorizationDenial)
        => new(
            code,
            TaskId: null,
            CurrentState: DeniedSafeOutcome,
            TerminalState: null,
            LastOperationId: null,
            LastFailureCategory: null,
            new WorkspaceStatusRetryEligibility(false, DeniedSafeOutcome),
            RetryAfter: null,
            freshness,
            query.CorrelationId,
            authorizationDenial);

    private static TaskStatusQueryResultCode MapTenantAccess(TenantAccessAuthorizationResult authorization)
        => authorization.Outcome switch
        {
            TenantAccessOutcome.StaleProjection => TaskStatusQueryResultCode.ProjectionStale,
            TenantAccessOutcome.UnavailableProjection => TaskStatusQueryResultCode.ProjectionUnavailable,
            TenantAccessOutcome.UnknownTenant or TenantAccessOutcome.Denied or TenantAccessOutcome.DisabledTenant => TaskStatusQueryResultCode.NotFoundSafe,
            TenantAccessOutcome.MissingAuthoritativeTenant => TaskStatusQueryResultCode.AuthenticationRequired,
            TenantAccessOutcome.MalformedEvidence or TenantAccessOutcome.ReplayConflict => TaskStatusQueryResultCode.ReadModelUnavailable,
            _ => TaskStatusQueryResultCode.AuthorizationDenied,
        };

    private static bool IsCanonicalIdentifier(string value)
        => value.Length <= 128
        && value.All(static c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.');

    private static bool IsContractShapedSnapshot(TaskStatusReadModelSnapshot snapshot, out string reasonCode)
    {
        if (snapshot.Freshness is null || snapshot.RetryEligibility is null)
        {
            reasonCode = "projection_malformed";
            return false;
        }

        if (!LifecycleStates.Contains(snapshot.CurrentState)
            || (snapshot.TerminalState is not null && !LifecycleStates.Contains(snapshot.TerminalState)))
        {
            reasonCode = "lifecycle_state_invalid";
            return false;
        }

        if (snapshot.LastOperationId is not null && !IsCanonicalIdentifier(snapshot.LastOperationId))
        {
            reasonCode = "operation_identifier_invalid";
            return false;
        }

        if (snapshot.LastFailureCategory is not null && !CanonicalErrorCategories.Contains(snapshot.LastFailureCategory))
        {
            reasonCode = "canonical_category_invalid";
            return false;
        }

        if (!IsFreshness(snapshot.Freshness)
            || !IsRetryEligibility(snapshot.RetryEligibility)
            || !IsRetryAfter(snapshot.RetryAfter))
        {
            reasonCode = "projection_malformed";
            return false;
        }

        reasonCode = string.Empty;
        return true;
    }

    private static bool IsFreshness(FolderLifecycleFreshness freshness)
        => string.Equals(freshness.ReadConsistency, EventuallyConsistent, StringComparison.Ordinal)
        && (freshness.ProjectionWatermark is null || IsCanonicalIdentifier(freshness.ProjectionWatermark))
        && (freshness.ReasonCode is null || IsReasonCode(freshness.ReasonCode));

    private static bool IsRetryEligibility(WorkspaceStatusRetryEligibility eligibility)
        => eligibility.AdvisoryOnly && IsReasonCode(eligibility.ReasonCode);

    private static bool IsRetryAfter(WorkspaceStatusRetryAfter? retryAfter)
        => retryAfter is null || retryAfter.AdvisoryOnly && retryAfter.RetryAfterSeconds is >= 1 and <= 3600;

    private static bool IsReasonCode(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 80
        && value[0] is >= 'a' and <= 'z'
        && value.All(static c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');

    private static bool HasValue(string? value)
        => !string.IsNullOrWhiteSpace(value);

    private static bool HasClientControlledMismatch(
        string authoritativeValue,
        IReadOnlyDictionary<string, string?>? clientValues)
        => clientValues is not null
        && clientValues.Values.Any(value =>
            !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value.Trim(), authoritativeValue, StringComparison.Ordinal));

    private static bool Matches(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);
}
