using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.FolderAccess;

/// <summary>
/// Lists the metadata-only folder ACL entries for a folder. Authorization-before-read, tenant-scoped,
/// safe denial. Production reads the durable EventStore folder permission view after authorization.
/// Only overrides whose action maps to a REST permission level are surfaced.
/// </summary>
public sealed class ListFolderAclEntriesQueryHandler(
    LayeredFolderAuthorizationService authorizationService,
    IFolderRepository repository,
    IUtcClock clock,
    ILogger<ListFolderAclEntriesQueryHandler>? logger = null,
    IFolderAclReadModel? folderAclReadModel = null)
{
    /// <summary>Action token authorizing the ACL read (folder metadata read).</summary>
    public const string ActionToken = "read_metadata";

    private const string ActorPresentIdentifier = "actor_present";
    private const string EventuallyConsistent = "eventually_consistent";

    private readonly LayeredFolderAuthorizationService _authorizationService =
        authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    private readonly IFolderRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IUtcClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly ILogger<ListFolderAclEntriesQueryHandler> _logger = logger ?? NullLogger<ListFolderAclEntriesQueryHandler>.Instance;
    private readonly IFolderAclReadModel? _folderAclReadModel = folderAclReadModel;

    /// <summary>
    /// Handles the query.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ACL entries or a safe denial.</returns>
    public async Task<ListFolderAclEntriesQueryResult> HandleAsync(
        ListFolderAclEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        FolderLifecycleFreshness deniedFreshness = FolderLifecycleFreshness.SafeUnavailable(_clock.UtcNow, "denied_safe");

        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId) || string.IsNullOrWhiteSpace(query.PrincipalId))
        {
            return Safe(ListFolderAclEntriesQueryResultCode.AuthenticationRequired, deniedFreshness, query, null);
        }

        if (string.IsNullOrWhiteSpace(query.FolderId)
            || query.FolderId == FolderStreamName.ReservedOrganizationPermissionScope)
        {
            return Safe(ListFolderAclEntriesQueryResultCode.NotFoundSafe, deniedFreshness, query, null);
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
        FolderState? state = null;
        FolderPermissionProjection? projection = null;
        try
        {
            if (_folderAclReadModel is not null)
            {
                projection = await _folderAclReadModel.GetCurrentAsync(allowed.AuthoritativeTenantId,
                    query.FolderId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                state = _repository.Load(_repository.CreateStreamName(allowed.AuthoritativeTenantId, query.FolderId));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Folder ACL read failed; returning ReadModelUnavailable. Exception type: {ExceptionType}",
                ex.GetType().FullName);
            return Safe(
                ListFolderAclEntriesQueryResultCode.ReadModelUnavailable,
                FolderLifecycleFreshness.SafeUnavailable(_clock.UtcNow, "read_model_unavailable"),
                query,
                null);
        }

        if (projection is not null ? !projection.IsCreated || projection.IsArchived
            : state is null || !state.IsCreated || state.LifecycleState == FolderLifecycleState.Archived)
        {
            return Safe(ListFolderAclEntriesQueryResultCode.NotFoundSafe, deniedFreshness, query, null);
        }

        List<FolderAclEntryView> entries = [];
        IEnumerable<(FolderAccessPrincipalKind Kind, string PrincipalId, string Action, bool IsGranted)> overrides =
            projection is not null
                ? projection.Overrides.Select(static item => (item.PrincipalKind, item.PrincipalId,
                    item.Action, item.IsGranted))
                : state!.AccessOverrides.Select(static item => (item.Key.PrincipalKind,
                    item.Key.PrincipalId, item.Key.Action, item.Value.IsGranted));
        foreach ((FolderAccessPrincipalKind kind, string principalId, string action, bool isGranted) in overrides)
        {
            string? permissionLevel = FolderAclContract.ActionToPermissionLevel(action);
            if (permissionLevel is null)
            {
                continue;
            }

            entries.Add(new FolderAclEntryView(
                FolderAclContract.DeriveAclEntryId(new FolderAccessEntryKey(allowed.AuthoritativeTenantId,
                    query.FolderId, kind, principalId, action).PrincipalKindToken, principalId, permissionLevel),
                FolderAclContract.FormatSubjectRef(kind, principalId),
                permissionLevel,
                isGranted ? "grant" : "revoke"));
        }

        IReadOnlyList<FolderAclEntryView> ordered = [.. entries.OrderBy(static e => e.AclEntryId, StringComparer.Ordinal)];
        FolderLifecycleFreshness freshness = new(
            EventuallyConsistent,
            _clock.UtcNow,
            allowed.FreshnessWatermark,
            Stale: false,
            ReasonCode: null);

        return new ListFolderAclEntriesQueryResult(
            ListFolderAclEntriesQueryResultCode.Allowed,
            ordered,
            freshness,
            query.CorrelationId,
            AuthorizationDenial: null);
    }

    private static ListFolderAclEntriesQueryResult Safe(
        ListFolderAclEntriesQueryResultCode code,
        FolderLifecycleFreshness freshness,
        ListFolderAclEntriesQuery query,
        LayeredFolderAuthorizationResult? authorizationDenial)
        => new(code, [], freshness, query.CorrelationId, authorizationDenial);

    private static ListFolderAclEntriesQueryResultCode MapAuthorizationDenial(LayeredFolderAuthorizationResult authorization)
        => authorization.Decision.OutcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied => ListFolderAclEntriesQueryResultCode.AuthenticationRequired,
            LayeredAuthorizationOutcomeCodes.SafeNotFound or LayeredAuthorizationOutcomeCodes.FolderAclDenied => ListFolderAclEntriesQueryResultCode.NotFoundSafe,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable
                or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable => ListFolderAclEntriesQueryResultCode.ProjectionUnavailable,
            LayeredAuthorizationOutcomeCodes.TenantProjectionStale
                or LayeredAuthorizationOutcomeCodes.FolderAclStale => ListFolderAclEntriesQueryResultCode.ProjectionStale,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied when authorization.Decision.Retryable => ListFolderAclEntriesQueryResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied
                or LayeredAuthorizationOutcomeCodes.ClaimTransformDenied
                or LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied
                or LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed
                or LayeredAuthorizationOutcomeCodes.TenantAccessDenied => ListFolderAclEntriesQueryResultCode.AuthorizationDenied,
            _ => ListFolderAclEntriesQueryResultCode.ReadModelUnavailable,
        };
}
