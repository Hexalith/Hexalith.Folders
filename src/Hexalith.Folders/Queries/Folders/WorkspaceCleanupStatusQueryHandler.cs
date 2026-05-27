using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.Folders;

public sealed class WorkspaceCleanupStatusQueryHandler(
    LayeredFolderAuthorizationService authorizationService,
    IWorkspaceCleanupStatusReadModel readModel,
    IUtcClock clock,
    ILogger<WorkspaceCleanupStatusQueryHandler>? logger = null)
{
    public const string ActionToken = "read_workspace_cleanup_status";
    private const string ActorPresentIdentifier = "actor_present";
    private const string DeniedSafeOutcome = "denied_safe";
    private const string ReadYourWrites = "read_your_writes";

    private static readonly HashSet<string> CleanupStatuses = new(StringComparer.Ordinal)
    {
        "pending",
        "succeeded",
        "failed",
        "status_only",
    };

    private readonly ILogger<WorkspaceCleanupStatusQueryHandler> _logger = logger ?? NullLogger<WorkspaceCleanupStatusQueryHandler>.Instance;

    public async Task<WorkspaceCleanupStatusQueryResult> HandleAsync(
        WorkspaceCleanupStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        FolderLifecycleFreshness deniedFreshness = new(ReadYourWrites, clock.UtcNow, null, Stale: true, "denied_safe");
        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(query.PrincipalId))
        {
            return SafeResult(WorkspaceCleanupStatusQueryResultCode.AuthenticationRequired, deniedFreshness, query, null);
        }

        if (string.IsNullOrWhiteSpace(query.FolderId)
            || string.IsNullOrWhiteSpace(query.WorkspaceId)
            || !IsCanonicalIdentifier(query.FolderId)
            || !IsCanonicalIdentifier(query.WorkspaceId)
            || (query.CorrelationId is not null && !IsCanonicalIdentifier(query.CorrelationId))
            || (query.TaskId is not null && !IsCanonicalIdentifier(query.TaskId)))
        {
            return SafeResult(WorkspaceCleanupStatusQueryResultCode.NotFoundSafe, deniedFreshness, query, null);
        }

        LayeredFolderAuthorizationResult authorization = await authorizationService.AuthorizeAsync(
            new LayeredFolderAuthorizationContext(
                query.AuthoritativeTenantId,
                query.PrincipalId,
                ActorPresentIdentifier,
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
        WorkspaceCleanupStatusReadModelResult readModelResult;
        try
        {
            readModelResult = await readModel.GetAsync(
                new WorkspaceCleanupStatusReadModelRequest(
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
                "Workspace cleanup status read-model call failed; returning ReadModelUnavailable. Exception type: {ExceptionType}",
                ex.GetType().FullName);
            return SafeResult(
                WorkspaceCleanupStatusQueryResultCode.ReadModelUnavailable,
                new FolderLifecycleFreshness(ReadYourWrites, clock.UtcNow, null, Stale: true, "read_model_unavailable"),
                query,
                null);
        }

        return readModelResult.Status switch
        {
            WorkspaceCleanupStatusReadModelStatus.Available when readModelResult.Snapshot is not null =>
                Compute(query, allowed, readModelResult.Snapshot),
            WorkspaceCleanupStatusReadModelStatus.Available =>
                SafeResult(WorkspaceCleanupStatusQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = "projection_malformed" }, query, null),
            WorkspaceCleanupStatusReadModelStatus.Stale =>
                SafeResult(WorkspaceCleanupStatusQueryResultCode.ProjectionStale, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_stale" }, query, null),
            WorkspaceCleanupStatusReadModelStatus.Unavailable =>
                SafeResult(WorkspaceCleanupStatusQueryResultCode.ProjectionUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_unavailable" }, query, null),
            WorkspaceCleanupStatusReadModelStatus.Malformed =>
                SafeResult(WorkspaceCleanupStatusQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_malformed" }, query, null),
            WorkspaceCleanupStatusReadModelStatus.NotFound =>
                SafeResult(WorkspaceCleanupStatusQueryResultCode.NotFoundSafe, readModelResult.Freshness, query, null),
            _ => SafeResult(WorkspaceCleanupStatusQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true }, query, null),
        };
    }

    private WorkspaceCleanupStatusQueryResult Compute(
        WorkspaceCleanupStatusQuery query,
        LayeredFolderAuthorizationAllowedContext allowed,
        WorkspaceCleanupStatusReadModelSnapshot snapshot)
    {
        WorkspaceCleanupStatusQueryResult? incompatible = ValidateSnapshotCompatibility(query, allowed, snapshot);
        if (incompatible is not null)
        {
            return incompatible;
        }

        FolderLifecycleFreshness freshness = snapshot.Freshness.ReadConsistency == ReadYourWrites
            ? snapshot.Freshness
            : snapshot.Freshness with { ReadConsistency = ReadYourWrites };

        return new(
            WorkspaceCleanupStatusQueryResultCode.Allowed,
            snapshot.FolderId,
            snapshot.WorkspaceId,
            snapshot.TaskId,
            snapshot.Status,
            snapshot.ReasonCode,
            snapshot.RetryEligibility,
            freshness,
            snapshot.CorrelationId ?? query.CorrelationId,
            snapshot.ObservedAt,
            snapshot.LastAttemptedAt,
            AuthorizationDenial: null);
    }

    private WorkspaceCleanupStatusQueryResult? ValidateSnapshotCompatibility(
        WorkspaceCleanupStatusQuery query,
        LayeredFolderAuthorizationAllowedContext allowed,
        WorkspaceCleanupStatusReadModelSnapshot snapshot)
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

        if (HasValue(snapshot.TaskId) && !Matches(snapshot.TaskId, query.TaskId))
        {
            return Unavailable(query, snapshot.Freshness, "task_mismatch");
        }

        if (HasValue(snapshot.CorrelationId) && !Matches(snapshot.CorrelationId, query.CorrelationId))
        {
            return Unavailable(query, snapshot.Freshness, "correlation_mismatch");
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
                WorkspaceCleanupStatusQueryResultCode.ProjectionStale,
                snapshot.Freshness with { Stale = true, ReasonCode = snapshot.Freshness.ReasonCode ?? "projection_stale" },
                query,
                null);
        }

        return null;
    }

    private static WorkspaceCleanupStatusQueryResult Unavailable(
        WorkspaceCleanupStatusQuery query,
        FolderLifecycleFreshness freshness,
        string reasonCode)
        => SafeResult(
            WorkspaceCleanupStatusQueryResultCode.ReadModelUnavailable,
            freshness with { Stale = true, ReasonCode = SafeReasonCode(freshness.ReasonCode, reasonCode) },
            query,
            null);

    private static WorkspaceCleanupStatusQueryResult SafeResult(
        WorkspaceCleanupStatusQueryResultCode code,
        FolderLifecycleFreshness freshness,
        WorkspaceCleanupStatusQuery query,
        LayeredFolderAuthorizationResult? authorizationDenial)
        => new(
            code,
            FolderId: null,
            WorkspaceId: null,
            TaskId: null,
            Status: DeniedSafeOutcome,
            ReasonCode: DeniedSafeOutcome,
            RetryEligibility: new WorkspaceStatusRetryEligibility(false, DeniedSafeOutcome),
            Freshness: freshness,
            CorrelationId: query.CorrelationId,
            ObservedAt: null,
            LastAttemptedAt: null,
            authorizationDenial);

    private static WorkspaceCleanupStatusQueryResultCode MapAuthorizationDenial(LayeredFolderAuthorizationResult authorization)
        => authorization.Decision.OutcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied => WorkspaceCleanupStatusQueryResultCode.AuthenticationRequired,
            LayeredAuthorizationOutcomeCodes.SafeNotFound or LayeredAuthorizationOutcomeCodes.FolderAclDenied => WorkspaceCleanupStatusQueryResultCode.NotFoundSafe,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable
                or LayeredAuthorizationOutcomeCodes.TenantProjectionStale
                or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable
                or LayeredAuthorizationOutcomeCodes.FolderAclStale => WorkspaceCleanupStatusQueryResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied when authorization.Decision.Retryable => WorkspaceCleanupStatusQueryResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied
                or LayeredAuthorizationOutcomeCodes.ClaimTransformDenied
                or LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied
                or LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed
                or LayeredAuthorizationOutcomeCodes.TenantAccessDenied => WorkspaceCleanupStatusQueryResultCode.AuthorizationDenied,
            _ => WorkspaceCleanupStatusQueryResultCode.ReadModelUnavailable,
        };

    private static bool IsContractShapedSnapshot(
        WorkspaceCleanupStatusReadModelSnapshot snapshot,
        DateTimeOffset now,
        out string reasonCode)
    {
        if (snapshot.Freshness is null || snapshot.RetryEligibility is null)
        {
            reasonCode = "projection_malformed";
            return false;
        }

        if (!CleanupStatuses.Contains(snapshot.Status))
        {
            reasonCode = "cleanup_status_invalid";
            return false;
        }

        if (!IsReasonCode(snapshot.ReasonCode))
        {
            reasonCode = "cleanup_reason_invalid";
            return false;
        }

        if (!IsRetryEligibility(snapshot.RetryEligibility, out reasonCode)
            || !IsReadYourWritesFreshness(snapshot.Freshness, now, out reasonCode))
        {
            return false;
        }

        if ((snapshot.TaskId is not null && !IsCanonicalIdentifier(snapshot.TaskId))
            || (snapshot.CorrelationId is not null && !IsCanonicalIdentifier(snapshot.CorrelationId)))
        {
            reasonCode = "cleanup_scope_identifier_invalid";
            return false;
        }

        if ((snapshot.ObservedAt is not null && snapshot.ObservedAt > now)
            || (snapshot.LastAttemptedAt is not null && snapshot.LastAttemptedAt > now))
        {
            reasonCode = "freshness_observed_in_future";
            return false;
        }

        reasonCode = string.Empty;
        return true;
    }

    private static FolderLifecycleFreshness SafeFreshness(
        FolderLifecycleFreshness? freshness,
        DateTimeOffset now)
        => freshness ?? new FolderLifecycleFreshness(ReadYourWrites, now, null, Stale: true, "projection_malformed");

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

    private static bool IsCanonicalIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(static c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-');

    private static bool IsReasonCode(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 80
        && value[0] is >= 'a' and <= 'z'
        && value.All(static c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');

    private static string SafeReasonCode(string? candidate, string fallback)
        => IsReasonCode(candidate) ? candidate! : fallback;

    private static bool HasValue(string? value)
        => !string.IsNullOrWhiteSpace(value);

    private static bool Matches(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);
}
