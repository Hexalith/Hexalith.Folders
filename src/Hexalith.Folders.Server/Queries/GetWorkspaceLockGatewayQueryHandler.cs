using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.Folders;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized GetWorkspaceLock projection through the Gateway.</summary>
internal sealed class GetWorkspaceLockGatewayQueryHandler(GatewayQueryExecutionSupport support,
    WorkspaceLockStatusQueryHandler handler)
    : GatewayQueryHandler<GetWorkspaceLock>(support, ReadYourWrites,
        WorkspaceLockStatusQueryHandler.ActionToken, static payload => payload.FolderId)
{
    /// <inheritdoc/>
    protected override string? WorkspaceScope(GetWorkspaceLock payload) => payload.WorkspaceId;

    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, GetWorkspaceLock payload,
        GatewayQueryPage page, CancellationToken token)
    {
        WorkspaceLockStatusQueryResult result = await handler.HandleAsync(new WorkspaceLockStatusQuery(
            payload.FolderId, payload.WorkspaceId, query.TenantId, query.UserId,
            Evidence(query), query.CorrelationId, null, null, null), token).ConfigureAwait(false);
        if (result.Code != WorkspaceLockStatusQueryResultCode.Allowed)
        {
            return result.Code is WorkspaceLockStatusQueryResultCode.ProjectionStale or WorkspaceLockStatusQueryResultCode.ProjectionUnavailable or WorkspaceLockStatusQueryResultCode.ReadModelUnavailable
                ? Unavailable : Denied;
        }

        if (string.IsNullOrWhiteSpace(result.WorkspaceId))
        {
            return Unavailable;
        }

        WorkspaceLockRetryEligibility retry = result.RetryEligibility;
        return Success(new
        {
            workspaceReference = new
            {
                value = result.WorkspaceId,
                classification = "operator_sanitized",
                redaction = new { visibility = "metadata_only", reasonCode = "not_redacted" },
            },
            result.LockState,
            result.Lease,
            retryEligibility = new
            {
                retry.Retryable,
                retry.RetryAfterSeconds,
                retry.ReasonCode,
                retry.CorrelationId,
                retry.TaskId,
                retry.CurrentState,
                freshness = ProjectFreshness(retry.Freshness, ReadYourWrites),
            },
        }, result.Freshness);
    }
}
