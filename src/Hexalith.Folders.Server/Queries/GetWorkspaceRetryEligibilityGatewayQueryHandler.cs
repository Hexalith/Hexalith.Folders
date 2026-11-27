using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.Folders;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized GetWorkspaceRetryEligibility projection through the Gateway.</summary>
internal sealed class GetWorkspaceRetryEligibilityGatewayQueryHandler(GatewayQueryExecutionSupport support,
    WorkspaceLockStatusQueryHandler handler)
    : GatewayQueryHandler<GetWorkspaceRetryEligibility>(support, EventuallyConsistent,
        WorkspaceStatusQueryHandler.ActionToken, static payload => payload.FolderId, historicalAction: WorkspaceLockStatusQueryHandler.ActionToken)
{
    /// <inheritdoc/>
    protected override string? TaskScope(GetWorkspaceRetryEligibility payload) => payload.TaskId;

    /// <inheritdoc/>
    protected override string? WorkspaceScope(GetWorkspaceRetryEligibility payload) => payload.WorkspaceId;

    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, GetWorkspaceRetryEligibility payload,
        GatewayQueryPage page, CancellationToken token)
    {
        WorkspaceLockStatusQueryResult result = await handler.HandleAsync(new WorkspaceLockStatusQuery(
            payload.FolderId, payload.WorkspaceId, query.TenantId, query.UserId,
            Evidence(query), query.CorrelationId, payload.TaskId, null, null), token).ConfigureAwait(false);
        if (result.Code != WorkspaceLockStatusQueryResultCode.Allowed)
        {
            return result.Code is WorkspaceLockStatusQueryResultCode.ProjectionStale or WorkspaceLockStatusQueryResultCode.ProjectionUnavailable or WorkspaceLockStatusQueryResultCode.ReadModelUnavailable
                ? Unavailable : Denied;
        }

        WorkspaceLockRetryEligibility retry = result.RetryEligibility;
        if (string.IsNullOrWhiteSpace(retry.CurrentState) || string.IsNullOrWhiteSpace(retry.ReasonCode))
        {
            return Unavailable;
        }

        return Success(new
        {
            retry.Retryable,
            retry.RetryAfterSeconds,
            retry.ReasonCode,
            correlationId = retry.CorrelationId ?? query.CorrelationId,
            taskId = retry.TaskId ?? payload.TaskId,
            retry.CurrentState,
        }, retry.Freshness);
    }
}
