using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.Folders;

public sealed class BranchRefPolicyQueryHandler(
    LayeredFolderAuthorizationService authorizationService,
    IBranchRefPolicyReadModel readModel,
    IUtcClock clock,
    ILogger<BranchRefPolicyQueryHandler>? logger = null)
{
    private const string ActionToken = "read_branch_ref_policy";
    private const string ActorPresentIdentifier = "actor_present";
    private const string AllowedOutcome = "allowed";
    private const string DeniedSafeOutcome = "denied_safe";
    private const string EventuallyConsistent = "eventually_consistent";

    private readonly ILogger<BranchRefPolicyQueryHandler> _logger = logger ?? NullLogger<BranchRefPolicyQueryHandler>.Instance;

    public async Task<BranchRefPolicyQueryResult> HandleAsync(
        BranchRefPolicyQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        FolderLifecycleFreshness deniedFreshness = FolderLifecycleFreshness.SafeUnavailable(clock.UtcNow, "denied_safe");
        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(query.PrincipalId))
        {
            return SafeResult(BranchRefPolicyQueryResultCode.AuthenticationRequired, deniedFreshness, query, null);
        }

        if (string.IsNullOrWhiteSpace(query.FolderId))
        {
            return SafeResult(BranchRefPolicyQueryResultCode.NotFoundSafe, deniedFreshness, query, null);
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
        BranchRefPolicyReadModelResult readModelResult;
        try
        {
            readModelResult = await readModel.GetAsync(
                new BranchRefPolicyReadModelRequest(
                    allowed.AuthoritativeTenantId,
                    query.FolderId,
                    query.PrincipalId,
                    ActionToken,
                    query.TaskId,
                    query.CorrelationId,
                    allowed.FreshnessWatermark,
                    EventuallyConsistent),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Branch/ref policy read-model call failed; returning ReadModelUnavailable. Exception type: {ExceptionType}",
                ex.GetType().FullName);
            return SafeResult(
                BranchRefPolicyQueryResultCode.ReadModelUnavailable,
                FolderLifecycleFreshness.SafeUnavailable(clock.UtcNow, "read_model_unavailable"),
                query,
                null);
        }

        return readModelResult.Status switch
        {
            BranchRefPolicyReadModelStatus.Available when readModelResult.Snapshot is not null =>
                Compute(query, allowed, readModelResult.Snapshot),
            BranchRefPolicyReadModelStatus.Available =>
                SafeResult(BranchRefPolicyQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = "projection_malformed" }, query, null),
            BranchRefPolicyReadModelStatus.Stale =>
                SafeResult(BranchRefPolicyQueryResultCode.ProjectionStale, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_stale" }, query, null),
            BranchRefPolicyReadModelStatus.Unavailable =>
                SafeResult(BranchRefPolicyQueryResultCode.ProjectionUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_unavailable" }, query, null),
            BranchRefPolicyReadModelStatus.Malformed =>
                SafeResult(BranchRefPolicyQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_malformed" }, query, null),
            BranchRefPolicyReadModelStatus.NotFound =>
                SafeResult(BranchRefPolicyQueryResultCode.NotFoundSafe, readModelResult.Freshness, query, null),
            _ => SafeResult(BranchRefPolicyQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true }, query, null),
        };
    }

    private BranchRefPolicyQueryResult Compute(
        BranchRefPolicyQuery query,
        LayeredFolderAuthorizationAllowedContext allowed,
        BranchRefPolicyReadModelSnapshot snapshot)
    {
        BranchRefPolicyQueryResult? incompatible = ValidateSnapshotCompatibility(query, allowed, snapshot);
        if (incompatible is not null)
        {
            return incompatible;
        }

        return new(
            BranchRefPolicyQueryResultCode.Allowed,
            snapshot.FolderId,
            snapshot.RepositoryBindingId,
            snapshot.PolicyRef,
            snapshot.DefaultRef,
            snapshot.AllowedRefPatterns,
            snapshot.ProtectedRefPatterns,
            AllowedOutcome,
            snapshot.Freshness,
            query.CorrelationId,
            query.TaskId,
            AuthorizationDenial: null);
    }

    private BranchRefPolicyQueryResult? ValidateSnapshotCompatibility(
        BranchRefPolicyQuery query,
        LayeredFolderAuthorizationAllowedContext allowed,
        BranchRefPolicyReadModelSnapshot snapshot)
    {
        if (snapshot.Freshness.ObservedAt > clock.UtcNow)
        {
            return Unavailable(query, snapshot.Freshness, "freshness_observed_in_future");
        }

        if (!Matches(snapshot.ManagedTenantId, allowed.AuthoritativeTenantId)
            || !Matches(snapshot.FolderId, query.FolderId))
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

    private static BranchRefPolicyQueryResult Unavailable(
        BranchRefPolicyQuery query,
        FolderLifecycleFreshness freshness,
        string reasonCode)
        => SafeResult(
            BranchRefPolicyQueryResultCode.ReadModelUnavailable,
            freshness with { Stale = true, ReasonCode = freshness.ReasonCode ?? reasonCode },
            query,
            null);

    private static BranchRefPolicyQueryResult SafeResult(
        BranchRefPolicyQueryResultCode code,
        FolderLifecycleFreshness freshness,
        BranchRefPolicyQuery query,
        LayeredFolderAuthorizationResult? authorizationDenial)
        => new(
            code,
            FolderId: null,
            RepositoryBindingId: null,
            PolicyRef: null,
            DefaultRef: null,
            AllowedRefPatterns: [],
            ProtectedRefPatterns: [],
            DeniedSafeOutcome,
            freshness,
            query.CorrelationId,
            query.TaskId,
            authorizationDenial);

    private static BranchRefPolicyQueryResultCode MapAuthorizationDenial(LayeredFolderAuthorizationResult authorization)
        => authorization.Decision.OutcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied => BranchRefPolicyQueryResultCode.AuthenticationRequired,
            LayeredAuthorizationOutcomeCodes.SafeNotFound or LayeredAuthorizationOutcomeCodes.FolderAclDenied => BranchRefPolicyQueryResultCode.NotFoundSafe,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable
                or LayeredAuthorizationOutcomeCodes.TenantProjectionStale
                or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable
                or LayeredAuthorizationOutcomeCodes.FolderAclStale => BranchRefPolicyQueryResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied when authorization.Decision.Retryable => BranchRefPolicyQueryResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied
                or LayeredAuthorizationOutcomeCodes.ClaimTransformDenied
                or LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied
                or LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed
                or LayeredAuthorizationOutcomeCodes.TenantAccessDenied => BranchRefPolicyQueryResultCode.AuthorizationDenied,
            _ => BranchRefPolicyQueryResultCode.ReadModelUnavailable,
        };

    private static bool HasValue(string? value)
        => !string.IsNullOrWhiteSpace(value);

    private static bool Matches(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);
}
