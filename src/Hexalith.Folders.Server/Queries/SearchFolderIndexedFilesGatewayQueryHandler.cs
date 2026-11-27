using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.ContextSearch;
using Hexalith.Folders.Queries.Folders;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized SearchFolderIndexedFiles projection through the Gateway.</summary>
internal sealed class SearchFolderIndexedFilesGatewayQueryHandler(GatewayQueryExecutionSupport support,
    ContextSearchQueryHandler handler)
    : GatewayQueryHandler<SearchFolderIndexedFiles>(support, EventuallyConsistent,
        ContextSearchQueryHandler.ActionToken, static payload => payload.FolderId)
{
    /// <inheritdoc/>
    protected override string? TaskScope(SearchFolderIndexedFiles payload) => payload.TaskId;

    /// <inheritdoc/>
    protected override string? WorkspaceScope(SearchFolderIndexedFiles payload) => payload.WorkspaceId;

    /// <inheritdoc/>
    protected override bool RequiresCurrentWorkspace => true;

    /// <inheritdoc/>
    protected override QueryResult? ValidatePaging(QueryPagingOptions? envelope, SearchFolderIndexedFiles payload,
        out GatewayQueryPage page)
    {
        QueryResult? error = ResolvePage(envelope, payload.Cursor, payload.Limit, 500, out page);
        return error ?? (ContextSearchQueryHandler.TryParseCursor(page.Cursor, out _) ? null : InvalidCursor);
    }

    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, SearchFolderIndexedFiles payload,
        GatewayQueryPage page, CancellationToken token)
    {
        ContextSearchQueryResult result = await handler.HandleGatewayAsync(new ContextSearchQuery(
            payload.FolderId, payload.WorkspaceId, query.TenantId, query.UserId, Evidence(query),
            query.CorrelationId, payload.TaskId, null, null, payload.QueryText, page.Limit, page.Cursor), token).ConfigureAwait(false);
        if (result.Code != ContextSearchResultCode.Allowed)
        {
            return result.Code switch
            {
                ContextSearchResultCode.QueryTimeout or ContextSearchResultCode.ReadModelUnavailable => Unavailable,
                ContextSearchResultCode.ResponseLimitExceeded => InvalidEnvelope,
                _ => Denied,
            };
        }

        return Success(new { result.Items, result.NextCursor, result.Limits }, result.Freshness,
            new QueryPagingMetadata(page.Limit, NextCursor: result.NextCursor, HasMore: result.NextCursor is not null));
    }
}
