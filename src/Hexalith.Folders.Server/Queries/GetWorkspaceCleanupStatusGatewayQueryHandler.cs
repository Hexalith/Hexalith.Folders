using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.Folders;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized GetWorkspaceCleanupStatus projection through the Gateway.</summary>
internal sealed class GetWorkspaceCleanupStatusGatewayQueryHandler(GatewayQueryExecutionSupport support,
    WorkspaceCleanupStatusQueryHandler handler)
    : GatewayQueryHandler<GetWorkspaceCleanupStatus>(support, ReadYourWrites,
        WorkspaceCleanupStatusQueryHandler.ActionToken, static payload => payload.FolderId)
{
    /// <inheritdoc/>
    protected override string? TaskScope(GetWorkspaceCleanupStatus payload) => payload.TaskId;

    /// <inheritdoc/>
    protected override string? WorkspaceScope(GetWorkspaceCleanupStatus payload) => payload.WorkspaceId;

    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, GetWorkspaceCleanupStatus payload,
        GatewayQueryPage page, CancellationToken token)
    {
        WorkspaceCleanupStatusQueryResult result = await handler.HandleAsync(new WorkspaceCleanupStatusQuery(
            payload.FolderId, payload.WorkspaceId, query.TenantId, query.UserId,
            Evidence(query), query.CorrelationId, payload.TaskId, null, null), token).ConfigureAwait(false);
        if (result.Code != WorkspaceCleanupStatusQueryResultCode.Allowed)
        {
            return result.Code is WorkspaceCleanupStatusQueryResultCode.ProjectionStale or WorkspaceCleanupStatusQueryResultCode.ProjectionUnavailable or WorkspaceCleanupStatusQueryResultCode.ReadModelUnavailable
                ? Unavailable : Denied;
        }

        return Success(new
        {
            result.FolderId,
            result.WorkspaceId,
            result.TaskId,
            result.Status,
            result.ReasonCode,
            result.RetryEligibility,
            result.CorrelationId,
            result.ObservedAt,
            result.LastAttemptedAt,
        }, result.Freshness);
    }
}
