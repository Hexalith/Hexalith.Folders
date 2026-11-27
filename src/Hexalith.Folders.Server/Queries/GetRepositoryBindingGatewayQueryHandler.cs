using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized GetRepositoryBinding projection through the Gateway.</summary>
internal sealed class GetRepositoryBindingGatewayQueryHandler(GatewayQueryExecutionSupport support,
    FolderLifecycleStatusQueryHandler handler)
    : GatewayQueryHandler<GetRepositoryBinding>(support, EventuallyConsistent,
        "read_metadata", static payload => payload.FolderId)
{
    /// <inheritdoc/>
    protected override string? SecondaryEntity(GetRepositoryBinding payload) => payload.RepositoryBindingId;

    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, GetRepositoryBinding payload,
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

        string? bindingState = FoldersDomainServiceEndpoints.MapRepositoryBindingState(result.LifecycleState);
        if (result.RepositoryBindingId != payload.RepositoryBindingId
            || string.IsNullOrWhiteSpace(result.ProviderBindingRef) || bindingState is null)
        {
            return Denied;
        }

        return Success(new
        {
            result.RepositoryBindingId,
            result.FolderId,
            result.ProviderBindingRef,
            bindingState,
            sensitiveMetadataTier = "tenant_sensitive",
        }, result.Freshness);
    }
}
