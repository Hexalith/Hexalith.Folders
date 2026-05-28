using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.Audit;

public sealed class OperationTimelineEntryQueryHandler(
    LayeredFolderAuthorizationService authorizationService,
    IOperationTimelineEntryReadModel readModel,
    IUtcClock clock,
    ILogger<OperationTimelineEntryQueryHandler>? logger = null)
{
    public const string ActionToken = "read_metadata";
    public const string OperationId = "GetOperationTimelineEntry";

    private const string ActorPresentIdentifier = "actor_present";
    private const string EventuallyConsistent = "eventually_consistent";

    private readonly ILogger<OperationTimelineEntryQueryHandler> _logger = logger ?? NullLogger<OperationTimelineEntryQueryHandler>.Instance;

    public async Task<OperationTimelineEntryQueryResult> HandleAsync(
        OperationTimelineEntryQuery query,
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

        if (string.IsNullOrWhiteSpace(query.FolderId) || string.IsNullOrWhiteSpace(query.TimelineEntryId))
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

        OperationTimelineEntryReadModelResult readModelResult;
        try
        {
            readModelResult = await readModel.GetAsync(
                new OperationTimelineEntryReadModelRequest(
                    allowed.AuthoritativeTenantId,
                    query.FolderId,
                    query.TimelineEntryId,
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
                new OperationTimelineEntryQueryResult(
                    AuditQueryResultCode.Allowed,
                    readModelResult.Snapshot.Entry,
                    readModelResult.Snapshot.Freshness,
                    query.CorrelationId,
                    query.TaskId,
                    OperationId,
                    AuthorizationDenial: null),
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

    private static OperationTimelineEntryQueryResult SafeResult(
        AuditQueryResultCode code,
        AuditFreshness freshness,
        OperationTimelineEntryQuery query,
        LayeredFolderAuthorizationResult? authorizationDenial)
        => new(
            code,
            Entry: null,
            freshness,
            query.CorrelationId,
            query.TaskId,
            OperationId,
            authorizationDenial);
}
