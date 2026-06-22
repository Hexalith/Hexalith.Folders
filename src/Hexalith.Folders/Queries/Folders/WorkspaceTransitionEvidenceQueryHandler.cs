using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.Folders;

/// <summary>
/// Reads a workspace's most recent C6 lifecycle transition evidence. Authorization-before-read,
/// tenant-scoped, safe denial, metadata-only.
/// </summary>
public sealed class WorkspaceTransitionEvidenceQueryHandler(
    LayeredFolderAuthorizationService authorizationService,
    IWorkspaceTransitionEvidenceReadModel readModel,
    IUtcClock clock,
    ILogger<WorkspaceTransitionEvidenceQueryHandler>? logger = null)
{
    /// <summary>Action token authorizing the transition-evidence read (folder metadata read).</summary>
    public const string ActionToken = "read_metadata";

    private const string ActorPresentIdentifier = "actor_present";
    private const string SnapshotPerTask = "snapshot_per_task";

    private readonly LayeredFolderAuthorizationService _authorizationService =
        authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    private readonly IWorkspaceTransitionEvidenceReadModel _readModel =
        readModel ?? throw new ArgumentNullException(nameof(readModel));
    private readonly IUtcClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly ILogger<WorkspaceTransitionEvidenceQueryHandler> _logger = logger ?? NullLogger<WorkspaceTransitionEvidenceQueryHandler>.Instance;

    /// <summary>
    /// Handles the query.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transition evidence or a safe denial.</returns>
    public async Task<WorkspaceTransitionEvidenceQueryResult> HandleAsync(
        WorkspaceTransitionEvidenceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        FolderLifecycleFreshness deniedFreshness = FolderLifecycleFreshness.SafeUnavailable(_clock.UtcNow, "denied_safe");

        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId) || string.IsNullOrWhiteSpace(query.PrincipalId))
        {
            return Safe(WorkspaceTransitionEvidenceQueryResultCode.AuthenticationRequired, deniedFreshness, query, null);
        }

        if (string.IsNullOrWhiteSpace(query.FolderId) || string.IsNullOrWhiteSpace(query.WorkspaceId))
        {
            return Safe(WorkspaceTransitionEvidenceQueryResultCode.NotFoundSafe, deniedFreshness, query, null);
        }

        LayeredFolderAuthorizationResult authorization = await _authorizationService.AuthorizeAsync(
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
            return Safe(MapAuthorizationDenial(authorization), deniedFreshness, query, authorization);
        }

        LayeredFolderAuthorizationAllowedContext allowed = authorization.AllowedContext;
        WorkspaceTransitionEvidenceSnapshot? snapshot;
        try
        {
            snapshot = await _readModel.GetAsync(
                new WorkspaceTransitionEvidenceReadModelRequest(
                    allowed.AuthoritativeTenantId,
                    query.FolderId,
                    query.WorkspaceId,
                    query.PrincipalId,
                    ActionToken,
                    query.TaskId,
                    query.CorrelationId,
                    allowed.FreshnessWatermark,
                    SnapshotPerTask),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Workspace transition-evidence read failed; returning ReadModelUnavailable. Exception type: {ExceptionType}",
                ex.GetType().FullName);
            return Safe(
                WorkspaceTransitionEvidenceQueryResultCode.ReadModelUnavailable,
                FolderLifecycleFreshness.SafeUnavailable(_clock.UtcNow, "read_model_unavailable"),
                query,
                null);
        }

        // Safe denial: a snapshot owned by a different tenant/workspace is indistinguishable from missing.
        if (snapshot is null
            || !string.Equals(snapshot.ManagedTenantId, allowed.AuthoritativeTenantId, StringComparison.Ordinal)
            || !string.Equals(snapshot.WorkspaceId, query.WorkspaceId, StringComparison.Ordinal)
            || !string.Equals(snapshot.FolderId, query.FolderId, StringComparison.Ordinal))
        {
            return Safe(WorkspaceTransitionEvidenceQueryResultCode.NotFoundSafe, deniedFreshness, query, null);
        }

        return new WorkspaceTransitionEvidenceQueryResult(
            WorkspaceTransitionEvidenceQueryResultCode.Allowed,
            snapshot,
            snapshot.Freshness,
            query.CorrelationId,
            query.TaskId,
            AuthorizationDenial: null);
    }

    private static WorkspaceTransitionEvidenceQueryResult Safe(
        WorkspaceTransitionEvidenceQueryResultCode code,
        FolderLifecycleFreshness freshness,
        WorkspaceTransitionEvidenceQuery query,
        LayeredFolderAuthorizationResult? authorizationDenial)
        => new(code, Snapshot: null, freshness, query.CorrelationId, query.TaskId, authorizationDenial);

    private static WorkspaceTransitionEvidenceQueryResultCode MapAuthorizationDenial(LayeredFolderAuthorizationResult authorization)
        => authorization.Decision.OutcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied => WorkspaceTransitionEvidenceQueryResultCode.AuthenticationRequired,
            LayeredAuthorizationOutcomeCodes.SafeNotFound or LayeredAuthorizationOutcomeCodes.FolderAclDenied => WorkspaceTransitionEvidenceQueryResultCode.NotFoundSafe,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable
                or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable => WorkspaceTransitionEvidenceQueryResultCode.ProjectionUnavailable,
            LayeredAuthorizationOutcomeCodes.TenantProjectionStale
                or LayeredAuthorizationOutcomeCodes.FolderAclStale => WorkspaceTransitionEvidenceQueryResultCode.ProjectionStale,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied when authorization.Decision.Retryable => WorkspaceTransitionEvidenceQueryResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied
                or LayeredAuthorizationOutcomeCodes.ClaimTransformDenied
                or LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied
                or LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed
                or LayeredAuthorizationOutcomeCodes.TenantAccessDenied => WorkspaceTransitionEvidenceQueryResultCode.AuthorizationDenied,
            _ => WorkspaceTransitionEvidenceQueryResultCode.ReadModelUnavailable,
        };
}
