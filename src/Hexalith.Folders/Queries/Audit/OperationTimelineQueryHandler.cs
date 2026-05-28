using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts.Projections.Audit;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.Audit;

public sealed class OperationTimelineQueryHandler(
    LayeredFolderAuthorizationService authorizationService,
    IOperationTimelineReadModel readModel,
    IUtcClock clock,
    ILogger<OperationTimelineQueryHandler>? logger = null)
{
    public const string ActionToken = "read_metadata";
    public const string OperationId = "ListOperationTimeline";
    public const string RetentionClassToken = "TODO(reference-pending):operation-timeline-retention";
    public const int MaxLimit = 100;
    public const int DefaultLimit = 50;

    private const string ActorPresentIdentifier = "actor_present";
    private const string EventuallyConsistent = "eventually_consistent";

    private readonly ILogger<OperationTimelineQueryHandler> _logger = logger ?? NullLogger<OperationTimelineQueryHandler>.Instance;

    public async Task<OperationTimelineQueryResult> HandleAsync(
        OperationTimelineQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        AuditFreshness deniedFreshness = AuditFreshness.SafeUnavailable(clock.UtcNow, "denied_safe");

        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(query.PrincipalId))
        {
            return SafeResult(AuditQueryResultCode.AuthenticationRequired, deniedFreshness, query, null);
        }

        if (string.IsNullOrWhiteSpace(query.FolderId))
        {
            return SafeResult(AuditQueryResultCode.NotFoundSafe, deniedFreshness, query, null);
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
            return SafeResult(AuditMapping.MapAuthorizationDenial(authorization), deniedFreshness, query, authorization);
        }

        LayeredFolderAuthorizationAllowedContext allowed = authorization.AllowedContext;
        int effectiveLimit = ResolveLimit(query.RequestedLimit);

        OperationTimelineReadModelResult readModelResult;
        try
        {
            readModelResult = await readModel.GetAsync(
                new OperationTimelineReadModelRequest(
                    allowed.AuthoritativeTenantId,
                    query.FolderId,
                    query.PrincipalId,
                    ActionToken,
                    query.Cursor,
                    effectiveLimit,
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
                "{OperationId} read-model call failed; returning ReadModelUnavailable. Exception type: {ExceptionType}",
                OperationId,
                ex.GetType().FullName);
            return SafeResult(
                AuditQueryResultCode.ReadModelUnavailable,
                AuditFreshness.SafeUnavailable(clock.UtcNow, "read_model_unavailable"),
                query,
                null);
        }

        return readModelResult.Status switch
        {
            AuditReadModelStatus.Available when readModelResult.Snapshot is not null =>
                Compose(query, readModelResult.Snapshot, query.RequestedLimit, effectiveLimit),
            AuditReadModelStatus.Available =>
                SafeResult(AuditQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = "projection_malformed" }, query, null),
            AuditReadModelStatus.Stale =>
                SafeResult(AuditQueryResultCode.ProjectionStale, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_stale" }, query, null),
            AuditReadModelStatus.Unavailable =>
                SafeResult(AuditQueryResultCode.ProjectionUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_unavailable" }, query, null),
            AuditReadModelStatus.Malformed =>
                SafeResult(AuditQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_malformed" }, query, null),
            AuditReadModelStatus.NotFound =>
                SafeResult(AuditQueryResultCode.NotFoundSafe, readModelResult.Freshness, query, null),
            _ => SafeResult(AuditQueryResultCode.ReadModelUnavailable, readModelResult.Freshness with { Stale = true }, query, null),
        };
    }

    private static int ResolveLimit(int? requested)
    {
        if (requested is null)
        {
            return DefaultLimit;
        }

        if (requested.Value < 1)
        {
            return 1;
        }

        return Math.Min(requested.Value, MaxLimit);
    }

    private static OperationTimelineQueryResult Compose(
        OperationTimelineQuery query,
        OperationTimelineReadModelSnapshot snapshot,
        int? requestedLimit,
        int effectiveLimit)
    {
        FreshnessMetadata freshness = new(
            snapshot.Freshness.ReadConsistency,
            snapshot.Freshness.ObservedAt,
            snapshot.Freshness.ProjectionWatermark,
            snapshot.Freshness.Stale,
            snapshot.Freshness.ReasonCode);

        PaginationMetadata page = new(
            effectiveLimit,
            snapshot.IsTruncated,
            snapshot.NextCursor,
            requestedLimit,
            snapshot.TruncatedReason);

        OperationTimelinePage body = new(snapshot.Entries, page, RetentionClassToken, freshness);

        return new OperationTimelineQueryResult(
            AuditQueryResultCode.Allowed,
            body,
            snapshot.Freshness,
            query.CorrelationId,
            query.TaskId,
            OperationId,
            AuthorizationDenial: null);
    }

    private static OperationTimelineQueryResult SafeResult(
        AuditQueryResultCode code,
        AuditFreshness freshness,
        OperationTimelineQuery query,
        LayeredFolderAuthorizationResult? authorizationDenial)
        => new(
            code,
            Page: null,
            freshness,
            query.CorrelationId,
            query.TaskId,
            OperationId,
            authorizationDenial);
}
