using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.ContextSearch;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized GetFolderIndexingStatus projection through the Gateway.</summary>
internal sealed class GetFolderIndexingStatusGatewayQueryHandler(GatewayQueryExecutionSupport support,
    FolderIndexingStatusQueryHandler handler)
    : GatewayQueryHandler<GetFolderIndexingStatus>(support, EventuallyConsistent,
        ContextSearchQueryHandler.ActionToken, static payload => payload.FolderId)
{
    /// <inheritdoc/>
    protected override string? TaskScope(GetFolderIndexingStatus payload) => payload.TaskId;

    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, GetFolderIndexingStatus payload,
        GatewayQueryPage page, CancellationToken token)
    {
        FolderIndexingStatusQueryResult result = await handler.HandleAsync(new FolderIndexingStatusQuery(
            payload.FolderId, query.TenantId, query.UserId, Evidence(query),
            query.CorrelationId, payload.TaskId, null, null), token).ConfigureAwait(false);
        if (result.Code != FolderIndexingStatusResultCode.Allowed)
        {
            return result.Code is FolderIndexingStatusResultCode.ReadModelUnavailable
                ? Unavailable : Denied;
        }

        return Success(new { result.Items, result.IsTruncated }, result.Freshness);
    }
}
