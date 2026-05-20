using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.Folders;

public sealed class FolderLifecycleStatusQueryHandler(
    LayeredFolderAuthorizationService authorizationService,
    IFolderLifecycleStatusReadModel readModel,
    IUtcClock clock,
    ILogger<FolderLifecycleStatusQueryHandler>? logger = null)
{
    private const string ActionToken = "read_metadata";
    private const string ActorPresentIdentifier = "actor_present";
    private const string AllowedOutcome = "allowed";
    private const string DeniedSafeOutcome = "denied_safe";
    private const string EventuallyConsistent = "eventually_consistent";
    private const string OperationId = "GetFolderLifecycleStatus";

    private readonly ILogger<FolderLifecycleStatusQueryHandler> _logger = logger ?? NullLogger<FolderLifecycleStatusQueryHandler>.Instance;

    public async Task<FolderLifecycleStatusQueryResult> HandleAsync(
        FolderLifecycleStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        FolderLifecycleFreshness deniedFreshness = FolderLifecycleFreshness.SafeUnavailable(
            clock.UtcNow,
            "denied_safe");

        // Authentication-class denials: missing authoritative tenant or principal.
        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(query.PrincipalId))
        {
            return SafeResult(
                FolderLifecycleStatusResultCode.AuthenticationRequired,
                deniedFreshness,
                query,
                authorizationDenial: null);
        }

        // Empty folder ID is a not-found-to-caller case, not an authentication failure.
        if (string.IsNullOrWhiteSpace(query.FolderId))
        {
            return SafeResult(
                FolderLifecycleStatusResultCode.NotFoundSafe,
                deniedFreshness,
                query,
                authorizationDenial: null);
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
            return SafeResult(
                MapAuthorizationDenial(authorization),
                deniedFreshness,
                query,
                authorization);
        }

        LayeredFolderAuthorizationAllowedContext allowed = authorization.AllowedContext;
        FolderLifecycleStatusReadModelResult readModelResult;
        try
        {
            readModelResult = await readModel.GetAsync(
                new FolderLifecycleStatusReadModelRequest(
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
            // Metadata-only logging: capture exception type to aid diagnostics without leaking
            // payloads, identifiers, or provider details through structured logs.
            _logger.LogWarning(
                ex,
                "Folder lifecycle-status read-model call failed; returning ReadModelUnavailable. Exception type: {ExceptionType}",
                ex.GetType().FullName);
            return SafeResult(
                FolderLifecycleStatusResultCode.ReadModelUnavailable,
                FolderLifecycleFreshness.SafeUnavailable(clock.UtcNow, "read_model_unavailable"),
                query,
                authorizationDenial: null);
        }

        return readModelResult.Status switch
        {
            FolderLifecycleStatusReadModelStatus.Available when readModelResult.Snapshot is not null =>
                Compute(query, allowed, readModelResult.Snapshot),
            FolderLifecycleStatusReadModelStatus.Available =>
                SafeResult(FolderLifecycleStatusResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = "projection_malformed" }, query, null),
            FolderLifecycleStatusReadModelStatus.Stale =>
                SafeResult(FolderLifecycleStatusResultCode.ProjectionStale, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_stale" }, query, null),
            FolderLifecycleStatusReadModelStatus.Unavailable =>
                SafeResult(FolderLifecycleStatusResultCode.ProjectionUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_unavailable" }, query, null),
            FolderLifecycleStatusReadModelStatus.Malformed =>
                SafeResult(FolderLifecycleStatusResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_malformed" }, query, null),
            FolderLifecycleStatusReadModelStatus.NotFound =>
                SafeResult(FolderLifecycleStatusResultCode.NotFoundSafe, readModelResult.Freshness, query, null),
            _ => SafeResult(FolderLifecycleStatusResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true }, query, null),
        };
    }

    private FolderLifecycleStatusQueryResult Compute(
        FolderLifecycleStatusQuery query,
        LayeredFolderAuthorizationAllowedContext allowed,
        FolderLifecycleStatusReadModelSnapshot snapshot)
    {
        FolderLifecycleStatusQueryResult? incompatible = ValidateSnapshotCompatibility(query, allowed, snapshot);
        if (incompatible is not null)
        {
            return incompatible;
        }

        return snapshot.LifecycleState switch
        {
            FolderLifecycleProjectionState.Active => ComputeActive(query, snapshot),
            FolderLifecycleProjectionState.Archived => ComputeArchived(query, snapshot),
            FolderLifecycleProjectionState.ArchiveUnsupported =>
                ArchiveUnsupportedResult(query, snapshot.Freshness),
            FolderLifecycleProjectionState.Missing =>
                SafeResult(FolderLifecycleStatusResultCode.NotFoundSafe, snapshot.Freshness, query, null),
            FolderLifecycleProjectionState.Stale =>
                SafeResult(FolderLifecycleStatusResultCode.ProjectionStale, snapshot.Freshness with { Stale = true, ReasonCode = snapshot.Freshness.ReasonCode ?? "lifecycle_stale" }, query, null),
            FolderLifecycleProjectionState.Unavailable =>
                SafeResult(FolderLifecycleStatusResultCode.ProjectionUnavailable, snapshot.Freshness with { Stale = true, ReasonCode = snapshot.Freshness.ReasonCode ?? "lifecycle_unavailable" }, query, null),
            FolderLifecycleProjectionState.Malformed =>
                Unavailable(query, snapshot.Freshness, "lifecycle_malformed"),
            FolderLifecycleProjectionState.Unknown =>
                Unavailable(query, snapshot.Freshness, "lifecycle_state_unknown"),
            _ => Unavailable(query, snapshot.Freshness, "lifecycle_state_unknown"),
        };
    }

    private FolderLifecycleStatusQueryResult? ValidateSnapshotCompatibility(
        FolderLifecycleStatusQuery query,
        LayeredFolderAuthorizationAllowedContext allowed,
        FolderLifecycleStatusReadModelSnapshot snapshot)
    {
        if (snapshot.Freshness.ObservedAt > clock.UtcNow)
        {
            return Unavailable(query, snapshot.Freshness, "freshness_observed_in_future");
        }

        if (!Matches(snapshot.ManagedTenantId, allowed.AuthoritativeTenantId))
        {
            return Unavailable(query, snapshot.Freshness, "tenant_mismatch");
        }

        if (!Matches(snapshot.FolderId, query.FolderId))
        {
            return Unavailable(query, snapshot.Freshness, "folder_mismatch");
        }

        FolderLifecycleEvidenceScope scope = snapshot.EvidenceScope;
        if (HasValue(scope.ManagedTenantId) && !Matches(scope.ManagedTenantId, allowed.AuthoritativeTenantId))
        {
            return Unavailable(query, snapshot.Freshness, "evidence_tenant_mismatch");
        }

        // Principal scope is mandatory: snapshots must be scoped per principal to avoid
        // cross-principal reuse (project-context "Critical Don't-Miss").
        if (!HasValue(scope.PrincipalId))
        {
            return Unavailable(query, snapshot.Freshness, "evidence_principal_missing");
        }

        if (!Matches(scope.PrincipalId, query.PrincipalId))
        {
            return Unavailable(query, snapshot.Freshness, "principal_mismatch");
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
                FolderLifecycleStatusResultCode.ProjectionStale,
                snapshot.Freshness with { Stale = true, ReasonCode = snapshot.Freshness.ReasonCode ?? "projection_stale" },
                query,
                null);
        }

        return null;
    }

    private static FolderLifecycleStatusQueryResult ComputeActive(
        FolderLifecycleStatusQuery query,
        FolderLifecycleStatusReadModelSnapshot snapshot)
        => snapshot.BindingStatus switch
        {
            FolderRepositoryBindingStatus.Unbound when HasNoBindingReferences(snapshot) =>
                Success(query, snapshot, "ready", archived: false, repositoryBindingId: null, providerBindingRef: null),
            FolderRepositoryBindingStatus.Unbound =>
                Unavailable(query, snapshot.Freshness, "unbound_binding_metadata_malformed"),
            FolderRepositoryBindingStatus.BindingRequested =>
                SuccessWithBinding(query, snapshot, "requested"),
            FolderRepositoryBindingStatus.Bound =>
                SuccessWithBinding(query, snapshot, "ready"),
            FolderRepositoryBindingStatus.Failed =>
                SuccessWithBinding(query, snapshot, "failed"),
            FolderRepositoryBindingStatus.UnknownProviderOutcome =>
                SuccessWithBinding(query, snapshot, "unknown_provider_outcome"),
            FolderRepositoryBindingStatus.ReconciliationRequired =>
                SuccessWithBinding(query, snapshot, "reconciliation_required"),
            FolderRepositoryBindingStatus.Unsupported =>
                Unavailable(query, snapshot.Freshness, "binding_state_unsupported"),
            _ => Unavailable(query, snapshot.Freshness, "binding_state_unknown"),
        };

    private static FolderLifecycleStatusQueryResult ComputeArchived(
        FolderLifecycleStatusQuery query,
        FolderLifecycleStatusReadModelSnapshot snapshot)
        => snapshot.BindingStatus switch
        {
            FolderRepositoryBindingStatus.Unbound when HasNoBindingReferences(snapshot) =>
                Success(query, snapshot, "inaccessible", archived: true, repositoryBindingId: null, providerBindingRef: null),
            FolderRepositoryBindingStatus.Unbound =>
                Unavailable(query, snapshot.Freshness, "unbound_binding_metadata_malformed"),
            FolderRepositoryBindingStatus.Bound =>
                SuccessWithBinding(query, snapshot, "inaccessible", archived: true),
            _ => Unavailable(query, snapshot.Freshness, "archived_binding_state_unsupported"),
        };

    private static FolderLifecycleStatusQueryResult SuccessWithBinding(
        FolderLifecycleStatusQuery query,
        FolderLifecycleStatusReadModelSnapshot snapshot,
        string lifecycleState,
        bool archived = false)
    {
        if (string.IsNullOrWhiteSpace(snapshot.RepositoryBindingId)
            || string.IsNullOrWhiteSpace(snapshot.ProviderBindingRef))
        {
            return Unavailable(query, snapshot.Freshness, "binding_metadata_malformed");
        }

        return Success(
            query,
            snapshot,
            lifecycleState,
            archived,
            snapshot.RepositoryBindingId,
            snapshot.ProviderBindingRef);
    }

    private static FolderLifecycleStatusQueryResult Success(
        FolderLifecycleStatusQuery query,
        FolderLifecycleStatusReadModelSnapshot snapshot,
        string lifecycleState,
        bool archived,
        string? repositoryBindingId,
        string? providerBindingRef)
        => new(
            FolderLifecycleStatusResultCode.Allowed,
            snapshot.FolderId,
            lifecycleState,
            archived,
            repositoryBindingId,
            providerBindingRef,
            AllowedOutcome,
            snapshot.Freshness,
            query.CorrelationId,
            query.TaskId,
            OperationId,
            AuthorizationDenial: null);

    private static FolderLifecycleStatusQueryResult Unavailable(
        FolderLifecycleStatusQuery query,
        FolderLifecycleFreshness freshness,
        string reasonCode)
        => SafeResult(
            FolderLifecycleStatusResultCode.ReadModelUnavailable,
            freshness with { Stale = true, ReasonCode = freshness.ReasonCode ?? reasonCode },
            query,
            authorizationDenial: null);

    private static FolderLifecycleStatusQueryResult ArchiveUnsupportedResult(
        FolderLifecycleStatusQuery query,
        FolderLifecycleFreshness freshness)
        => SafeResult(
            FolderLifecycleStatusResultCode.ArchiveStateUnsupported,
            freshness with { Stale = true, ReasonCode = freshness.ReasonCode ?? "archive_state_unsupported" },
            query,
            authorizationDenial: null);

    private static FolderLifecycleStatusQueryResult SafeResult(
        FolderLifecycleStatusResultCode code,
        FolderLifecycleFreshness freshness,
        FolderLifecycleStatusQuery query,
        LayeredFolderAuthorizationResult? authorizationDenial)
        => new(
            code,
            FolderId: null,
            LifecycleState: null,
            Archived: false,
            RepositoryBindingId: null,
            ProviderBindingRef: null,
            DeniedSafeOutcome,
            freshness,
            query.CorrelationId,
            query.TaskId,
            OperationId,
            authorizationDenial);

    private static FolderLifecycleStatusResultCode MapAuthorizationDenial(LayeredFolderAuthorizationResult authorization)
        => authorization.Decision.OutcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied => FolderLifecycleStatusResultCode.AuthenticationRequired,
            LayeredAuthorizationOutcomeCodes.SafeNotFound or LayeredAuthorizationOutcomeCodes.FolderAclDenied => FolderLifecycleStatusResultCode.NotFoundSafe,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable
                or LayeredAuthorizationOutcomeCodes.TenantProjectionStale
                or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable
                or LayeredAuthorizationOutcomeCodes.FolderAclStale => FolderLifecycleStatusResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied when authorization.Decision.Retryable => FolderLifecycleStatusResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied
                or LayeredAuthorizationOutcomeCodes.ClaimTransformDenied
                or LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied
                or LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed
                or LayeredAuthorizationOutcomeCodes.TenantAccessDenied => FolderLifecycleStatusResultCode.AuthorizationDenied,
            // Unknown outcome codes fail closed to ReadModelUnavailable rather than silently
            // downgrading to a 403 — protects against future outcome additions.
            _ => FolderLifecycleStatusResultCode.ReadModelUnavailable,
        };

    private static bool HasNoBindingReferences(FolderLifecycleStatusReadModelSnapshot snapshot)
        => string.IsNullOrWhiteSpace(snapshot.RepositoryBindingId)
            && string.IsNullOrWhiteSpace(snapshot.ProviderBindingRef);

    private static bool HasValue(string? value)
        => !string.IsNullOrWhiteSpace(value);

    private static bool Matches(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);
}
