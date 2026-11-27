using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.Folders;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized GetTaskStatus projection through the Gateway.</summary>
internal sealed class GetTaskStatusGatewayQueryHandler(GatewayQueryExecutionSupport support,
    TaskStatusQueryHandler handler)
    : GatewayQueryHandler<GetTaskStatus>(support, EventuallyConsistent,
        "query_status", static payload => payload.FolderId, historicalAction: TaskStatusQueryHandler.ActionToken)
{
    /// <inheritdoc/>
    protected override string? TaskScope(GetTaskStatus payload) => payload.TaskId;

    /// <inheritdoc/>
    protected override bool RequiresCurrentTask => false;

    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, GetTaskStatus payload,
        GatewayQueryPage page, CancellationToken token)
    {
        TaskStatusQueryResult result = await handler.HandleAsync(new TaskStatusQuery(
            payload.TaskId, query.TenantId, query.UserId, Evidence(query, TaskStatusQueryHandler.ActionToken),
            query.CorrelationId, new Dictionary<string, string?>(), payload.FolderId), token).ConfigureAwait(false);
        if (result.Code != TaskStatusQueryResultCode.Allowed)
        {
            return result.Code is TaskStatusQueryResultCode.ProjectionStale or TaskStatusQueryResultCode.ProjectionUnavailable or TaskStatusQueryResultCode.ReadModelUnavailable
                ? Unavailable : Denied;
        }

        return Success(new
        {
            result.TaskId,
            result.CurrentState,
            result.TerminalState,
            result.LastOperationId,
            result.LastFailureCategory,
            result.RetryEligibility,
            result.RetryAfter,
        }, result.Freshness);
    }
}
