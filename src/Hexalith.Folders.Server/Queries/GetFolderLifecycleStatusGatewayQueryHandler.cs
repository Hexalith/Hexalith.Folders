using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.Folders;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized GetFolderLifecycleStatus projection through the Gateway.</summary>
internal sealed class GetFolderLifecycleStatusGatewayQueryHandler(GatewayQueryExecutionSupport support,
    FolderLifecycleStatusQueryHandler handler)
    : GatewayQueryHandler<GetFolderLifecycleStatus>(support, EventuallyConsistent,
        "read_metadata", static payload => payload.FolderId)
{
    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, GetFolderLifecycleStatus payload,
        GatewayQueryPage page, CancellationToken token)
    {
        FolderLifecycleStatusQueryResult result = await handler.HandleAsync(new FolderLifecycleStatusQuery(
            payload.FolderId, query.TenantId, query.UserId, Evidence(query),
            query.CorrelationId, null, null, null), token).ConfigureAwait(false);
        if (result.Code != FolderLifecycleStatusResultCode.Allowed)
        {
            return result.Code is FolderLifecycleStatusResultCode.ProjectionStale or FolderLifecycleStatusResultCode.ProjectionUnavailable or FolderLifecycleStatusResultCode.ReadModelUnavailable
                ? Unavailable : Denied;
        }

        return Success(new
        {
            result.FolderId,
            result.LifecycleState,
            result.Archived,
            result.RepositoryBindingId,
            result.ProviderBindingRef,
        }, result.Freshness);
    }
}
