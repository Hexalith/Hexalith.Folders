using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.Folders;

public sealed class WorkspaceStatusQueryHandler(
    LayeredFolderAuthorizationService authorizationService,
    IWorkspaceStatusReadModel readModel,
    IUtcClock clock,
    ILogger<WorkspaceStatusQueryHandler>? logger = null)
{
    public const string ActionToken = "read_workspace_status";
    private const string ActorPresentIdentifier = "actor_present";
    private const string DeniedSafeOutcome = "denied_safe";
    private const string ReadYourWrites = "read_your_writes";
    private static readonly HashSet<string> AcceptedCommandStates = new(StringComparer.Ordinal)
    {
        "accepted",
        "failed",
        "completed",
    };

    private static readonly HashSet<string> CanonicalErrorCategories = new(StringComparer.Ordinal)
    {
        "success",
        "authentication_failure",
        "client_configuration_error",
        "credential_missing",
        "credential_reference_invalid",
        "tenant_access_denied",
        "cross_tenant_access_denied",
        "folder_acl_denied",
        "audit_access_denied",
        "validation_error",
        "idempotency_conflict",
        "provider_readiness_failed",
        "provider_permission_insufficient",
        "provider_unavailable",
        "provider_rate_limited",
        "repository_binding_unavailable",
        "branch_ref_policy_invalid",
        "workspace_not_ready",
        "workspace_preparation_failed",
        "workspace_locked",
        "lock_conflict",
        "lock_expired",
        "lock_not_owned",
        "stale_workspace",
        "authorization_revocation_detected",
        "repository_conflict",
        "duplicate_binding",
        "unsupported_provider_capability",
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

    private static readonly HashSet<string> ProjectionLagSources = new(StringComparer.Ordinal)
    {
        "accepted_command",
        "projection",
        "reconciliation",
        "unavailable",
    };

    private static readonly HashSet<string> ProjectedStateSources = new(StringComparer.Ordinal)
    {
        "accepted_command",
        "projection",
        "reconciliation",
        "redacted",
        "unavailable",
    };

    private static readonly HashSet<string> ProviderOutcomeStates = new(StringComparer.Ordinal)
    {
        "pending",
        "known_success",
        "known_failure",
        "unknown_provider_outcome",
        "reconciliation_required",
    };

    private readonly ILogger<WorkspaceStatusQueryHandler> _logger = logger ?? NullLogger<WorkspaceStatusQueryHandler>.Instance;

    public async Task<WorkspaceStatusQueryResult> HandleAsync(
        WorkspaceStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        FolderLifecycleFreshness deniedFreshness = new(ReadYourWrites, clock.UtcNow, null, Stale: true, "denied_safe");
        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(query.PrincipalId))
        {
            return SafeResult(WorkspaceStatusQueryResultCode.AuthenticationRequired, deniedFreshness, query, null);
        }

        if (string.IsNullOrWhiteSpace(query.FolderId) || string.IsNullOrWhiteSpace(query.WorkspaceId))
        {
            return SafeResult(WorkspaceStatusQueryResultCode.NotFoundSafe, deniedFreshness, query, null);
        }

        if (!IsCanonicalIdentifier(query.FolderId)
            || !IsCanonicalIdentifier(query.WorkspaceId)
            || (query.CorrelationId is not null && !IsCanonicalIdentifier(query.CorrelationId))
            || (query.TaskId is not null && !IsCanonicalIdentifier(query.TaskId)))
        {
            return SafeResult(WorkspaceStatusQueryResultCode.NotFoundSafe, deniedFreshness, query, null);
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
        WorkspaceStatusReadModelResult readModelResult;
        try
        {
            readModelResult = await readModel.GetAsync(
                new WorkspaceStatusReadModelRequest(
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
                "Workspace status read-model call failed; returning ReadModelUnavailable. Exception type: {ExceptionType}",
                ex.GetType().FullName);
            return SafeResult(
                WorkspaceStatusQueryResultCode.ReadModelUnavailable,
                new FolderLifecycleFreshness(ReadYourWrites, clock.UtcNow, null, Stale: true, "read_model_unavailable"),
                query,
                null);
        }

        return readModelResult.Status switch
        {
            WorkspaceStatusReadModelStatus.Available when readModelResult.Snapshot is not null =>
                Compute(query, allowed, readModelResult.Snapshot),
            WorkspaceStatusReadModelStatus.Available =>
                SafeResult(WorkspaceStatusQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = "projection_malformed" }, query, null),
            WorkspaceStatusReadModelStatus.Stale =>
                SafeResult(WorkspaceStatusQueryResultCode.ProjectionStale, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_stale" }, query, null),
            WorkspaceStatusReadModelStatus.Unavailable =>
                SafeResult(WorkspaceStatusQueryResultCode.ProjectionUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_unavailable" }, query, null),
            WorkspaceStatusReadModelStatus.Malformed =>
                SafeResult(WorkspaceStatusQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_malformed" }, query, null),
            WorkspaceStatusReadModelStatus.NotFound =>
                SafeResult(WorkspaceStatusQueryResultCode.NotFoundSafe, readModelResult.Freshness, query, null),
            _ => SafeResult(WorkspaceStatusQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true }, query, null),
        };
    }

    private WorkspaceStatusQueryResult Compute(
        WorkspaceStatusQuery query,
        LayeredFolderAuthorizationAllowedContext allowed,
        WorkspaceStatusReadModelSnapshot snapshot)
    {
        WorkspaceStatusQueryResult? incompatible = ValidateSnapshotCompatibility(query, allowed, snapshot);
        if (incompatible is not null)
        {
            return incompatible;
        }

        FolderLifecycleFreshness freshness = snapshot.Freshness.ReadConsistency == ReadYourWrites
            ? snapshot.Freshness
            : snapshot.Freshness with { ReadConsistency = ReadYourWrites };

        return new(
            WorkspaceStatusQueryResultCode.Allowed,
            snapshot.FolderId,
            snapshot.WorkspaceId,
            snapshot.CurrentState,
            snapshot.AcceptedCommandState,
            snapshot.ProjectedState,
            snapshot.ProviderOutcome,
            snapshot.RetryEligibility,
            snapshot.RetryAfter,
            freshness,
            snapshot.ProjectionLag,
            snapshot.LastFailureCategory,
            query.CorrelationId,
            query.TaskId,
            AuthorizationDenial: null);
    }

    private WorkspaceStatusQueryResult? ValidateSnapshotCompatibility(
        WorkspaceStatusQuery query,
        LayeredFolderAuthorizationAllowedContext allowed,
        WorkspaceStatusReadModelSnapshot snapshot)
    {
        DateTimeOffset now = clock.UtcNow;
        if (!IsContractShapedSnapshot(snapshot, now, out string? reasonCode))
        {
            return Unavailable(query, SafeFreshness(snapshot.Freshness, now), reasonCode);
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

        if (snapshot.Freshness.Stale)
        {
            return SafeResult(
                WorkspaceStatusQueryResultCode.ProjectionStale,
                snapshot.Freshness with { Stale = true, ReasonCode = snapshot.Freshness.ReasonCode ?? "projection_stale" },
                query,
                null);
        }

        return null;
    }

    private static WorkspaceStatusQueryResult Unavailable(
        WorkspaceStatusQuery query,
        FolderLifecycleFreshness freshness,
        string reasonCode)
        => SafeResult(
            WorkspaceStatusQueryResultCode.ReadModelUnavailable,
            freshness with { Stale = true, ReasonCode = freshness.ReasonCode ?? reasonCode },
            query,
            null);

    private static WorkspaceStatusQueryResult SafeResult(
        WorkspaceStatusQueryResultCode code,
        FolderLifecycleFreshness freshness,
        WorkspaceStatusQuery query,
        LayeredFolderAuthorizationResult? authorizationDenial)
        => new(
            code,
            FolderId: null,
            WorkspaceId: null,
            CurrentState: DeniedSafeOutcome,
            AcceptedCommandState: null,
            ProjectedState: null,
            ProviderOutcome: null,
            RetryEligibility: new WorkspaceStatusRetryEligibility(false, DeniedSafeOutcome),
            RetryAfter: null,
            Freshness: freshness,
            ProjectionLag: new WorkspaceProjectionLag(null, "unavailable"),
            LastFailureCategory: null,
            query.CorrelationId,
            query.TaskId,
            authorizationDenial);

    private static WorkspaceStatusQueryResultCode MapAuthorizationDenial(LayeredFolderAuthorizationResult authorization)
        => authorization.Decision.OutcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied => WorkspaceStatusQueryResultCode.AuthenticationRequired,
            LayeredAuthorizationOutcomeCodes.SafeNotFound or LayeredAuthorizationOutcomeCodes.FolderAclDenied => WorkspaceStatusQueryResultCode.NotFoundSafe,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable
                or LayeredAuthorizationOutcomeCodes.TenantProjectionStale
                or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable
                or LayeredAuthorizationOutcomeCodes.FolderAclStale => WorkspaceStatusQueryResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied when authorization.Decision.Retryable => WorkspaceStatusQueryResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied
                or LayeredAuthorizationOutcomeCodes.ClaimTransformDenied
                or LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied
                or LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed
                or LayeredAuthorizationOutcomeCodes.TenantAccessDenied => WorkspaceStatusQueryResultCode.AuthorizationDenied,
            _ => WorkspaceStatusQueryResultCode.ReadModelUnavailable,
        };

    private static bool IsContractShapedSnapshot(
        WorkspaceStatusReadModelSnapshot snapshot,
        DateTimeOffset now,
        out string reasonCode)
    {
        if (snapshot.Freshness is null
            || snapshot.ProjectedState is null
            || snapshot.ProviderOutcome is null
            || snapshot.RetryEligibility is null
            || snapshot.ProjectionLag is null)
        {
            reasonCode = "projection_malformed";
            return false;
        }

        if (!IsReadYourWritesFreshness(snapshot.Freshness, now, out reasonCode))
        {
            return false;
        }

        if (!IsLifecycleState(snapshot.CurrentState)
            || !IsLifecycleState(snapshot.ProjectedState.State))
        {
            reasonCode = "lifecycle_state_invalid";
            return false;
        }

        if (!ProjectedStateSources.Contains(snapshot.ProjectedState.StateSource))
        {
            reasonCode = "projected_state_source_invalid";
            return false;
        }

        if (snapshot.ProjectedState.ObservedAt > now)
        {
            reasonCode = "freshness_observed_in_future";
            return false;
        }

        if (!IsAcceptedCommandState(snapshot.AcceptedCommandState, now, out reasonCode)
            || !IsProviderOutcome(snapshot.ProviderOutcome, now, out reasonCode)
            || !IsRetryEligibility(snapshot.RetryEligibility, out reasonCode)
            || !IsRetryAfter(snapshot.RetryAfter, out reasonCode)
            || !IsProjectionLag(snapshot.ProjectionLag, out reasonCode)
            || !IsCanonicalCategory(snapshot.LastFailureCategory, out reasonCode))
        {
            return false;
        }

        reasonCode = string.Empty;
        return true;
    }

    private static FolderLifecycleFreshness SafeFreshness(
        FolderLifecycleFreshness? freshness,
        DateTimeOffset now)
        => freshness ?? new FolderLifecycleFreshness(ReadYourWrites, now, null, Stale: true, "projection_malformed");

    private static bool IsAcceptedCommandState(
        WorkspaceAcceptedCommandState? state,
        DateTimeOffset now,
        out string reasonCode)
    {
        if (state is null)
        {
            reasonCode = string.Empty;
            return true;
        }

        if (!IsCanonicalIdentifier(state.TaskId) || !IsCanonicalIdentifier(state.OperationId))
        {
            reasonCode = "accepted_command_identifier_invalid";
            return false;
        }

        if (!AcceptedCommandStates.Contains(state.State))
        {
            reasonCode = "accepted_command_state_invalid";
            return false;
        }

        if (state.AcceptedAt > now)
        {
            reasonCode = "freshness_observed_in_future";
            return false;
        }

        reasonCode = string.Empty;
        return true;
    }

    private static bool IsProviderOutcome(
        WorkspaceProviderOutcome outcome,
        DateTimeOffset now,
        out string reasonCode)
    {
        if (!IsCanonicalIdentifier(outcome.OperationId))
        {
            reasonCode = "provider_operation_identifier_invalid";
            return false;
        }

        if (!ProviderOutcomeStates.Contains(outcome.State))
        {
            reasonCode = "provider_outcome_state_invalid";
            return false;
        }

        if (!IsCanonicalCategory(outcome.SanitizedStatusClass, out reasonCode))
        {
            return false;
        }

        if (!IsProviderCorrelationReference(outcome.ProviderCorrelationReference))
        {
            reasonCode = "provider_correlation_reference_invalid";
            return false;
        }

        if (outcome.RetryEligibility is null
            || !IsRetryEligibility(outcome.RetryEligibility, out reasonCode)
            || !IsRetryAfter(outcome.RetryAfter, out reasonCode)
            || outcome.Freshness is null
            || !IsReadYourWritesFreshness(outcome.Freshness, now, out reasonCode))
        {
            return false;
        }

        reasonCode = string.Empty;
        return true;
    }

    private static bool IsProjectionLag(WorkspaceProjectionLag lag, out string reasonCode)
    {
        if (!ProjectionLagSources.Contains(lag.StateSource))
        {
            reasonCode = "projection_lag_source_invalid";
            return false;
        }

        if (lag.AgeMilliseconds < 0)
        {
            reasonCode = "projection_lag_invalid";
            return false;
        }

        reasonCode = string.Empty;
        return true;
    }

    private static bool IsReadYourWritesFreshness(
        FolderLifecycleFreshness freshness,
        DateTimeOffset now,
        out string reasonCode)
    {
        if (!string.Equals(freshness.ReadConsistency, ReadYourWrites, StringComparison.Ordinal))
        {
            reasonCode = "freshness_read_consistency_mismatch";
            return false;
        }

        if (freshness.ObservedAt > now)
        {
            reasonCode = "freshness_observed_in_future";
            return false;
        }

        if (freshness.ProjectionWatermark is not null && !IsCanonicalIdentifier(freshness.ProjectionWatermark))
        {
            reasonCode = "freshness_watermark_invalid";
            return false;
        }

        if (freshness.ReasonCode is not null && !IsReasonCode(freshness.ReasonCode))
        {
            reasonCode = "freshness_reason_invalid";
            return false;
        }

        reasonCode = string.Empty;
        return true;
    }

    private static bool IsRetryEligibility(
        WorkspaceStatusRetryEligibility eligibility,
        out string reasonCode)
    {
        if (!eligibility.AdvisoryOnly || !IsReasonCode(eligibility.ReasonCode))
        {
            reasonCode = "retry_eligibility_invalid";
            return false;
        }

        reasonCode = string.Empty;
        return true;
    }

    private static bool IsRetryAfter(WorkspaceStatusRetryAfter? retryAfter, out string reasonCode)
    {
        if (retryAfter is null)
        {
            reasonCode = string.Empty;
            return true;
        }

        if (!retryAfter.AdvisoryOnly || retryAfter.RetryAfterSeconds is < 1 or > 3600)
        {
            reasonCode = "retry_after_invalid";
            return false;
        }

        reasonCode = string.Empty;
        return true;
    }

    private static bool IsCanonicalCategory(string? category, out string reasonCode)
    {
        if (category is null)
        {
            reasonCode = string.Empty;
            return true;
        }

        if (!CanonicalErrorCategories.Contains(category))
        {
            reasonCode = "canonical_category_invalid";
            return false;
        }

        reasonCode = string.Empty;
        return true;
    }

    private static bool IsLifecycleState(string? value)
        => value is not null && LifecycleStates.Contains(value);

    private static bool IsCanonicalIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(static c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-');

    private static bool IsProviderCorrelationReference(string? value)
    {
        if (value is null
            || value.Length is < 16 or > 128
            || !value.StartsWith("provref_", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (char tailCharacter in value.AsSpan("provref_".Length))
        {
            if (!IsProviderReferenceTailChar(tailCharacter))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsReasonCode(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 80
        && value[0] is >= 'a' and <= 'z'
        && value.All(static c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');

    private static bool IsProviderReferenceTailChar(char value)
        => value is >= 'A' and <= 'Z'
        or >= 'a' and <= 'z'
        or >= '0' and <= '9'
        or '_'
        or '-';

    private static bool HasValue(string? value)
        => !string.IsNullOrWhiteSpace(value);

    private static bool Matches(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);
}
