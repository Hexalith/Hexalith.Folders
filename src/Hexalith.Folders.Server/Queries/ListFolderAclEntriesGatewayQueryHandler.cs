using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Queries.FolderAccess;
using Hexalith.Folders.Server;

using static Hexalith.Folders.Queries.Gateway.GatewayQueryExecutionSupport;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Serves the authorized ListFolderAclEntries projection through the Gateway.</summary>
internal sealed class ListFolderAclEntriesGatewayQueryHandler(GatewayQueryExecutionSupport support,
    ListFolderAclEntriesQueryHandler handler)
    : GatewayQueryHandler<ListFolderAclEntries>(support, EventuallyConsistent,
        "manage_folder_access", static payload => payload.FolderId, historicalAction: ListFolderAclEntriesQueryHandler.ActionToken)
{
    /// <inheritdoc/>
    protected override QueryResult? ValidatePaging(QueryPagingOptions? envelope, ListFolderAclEntries payload,
        out GatewayQueryPage page)
    {
        QueryResult? error = ResolvePage(envelope, payload.Cursor, payload.Limit, 1000, out page);
        // The REST contract currently supports no ACL filter vocabulary.
        return error ?? (payload.Filter is null ? null : InvalidEnvelope);
    }

    /// <inheritdoc/>
    protected override async Task<QueryResult> ReadAsync(QueryEnvelope query, ListFolderAclEntries payload,
        GatewayQueryPage page, CancellationToken token)
    {
        ListFolderAclEntriesQueryResult result = await handler.HandleAsync(new ListFolderAclEntriesQuery(
            query.TenantId, query.UserId, Evidence(query), payload.FolderId,
            query.CorrelationId, null, null, null), token).ConfigureAwait(false);
        if (result.Code != ListFolderAclEntriesQueryResultCode.Allowed)
        {
            return result.Code is ListFolderAclEntriesQueryResultCode.ProjectionStale or ListFolderAclEntriesQueryResultCode.ProjectionUnavailable or ListFolderAclEntriesQueryResultCode.ReadModelUnavailable
                ? Unavailable : Denied;
        }

        if (!FoldersDomainServiceEndpoints.TryPageFolderAcl(result.Entries, page.Cursor, page.Limit,
            out FolderAclEntryView[] entries, out string? nextCursor))
        {
            return InvalidCursor;
        }

        bool hasMore = nextCursor is not null;
        return Success(new
        {
            items = entries,
            page = new
            {
                cursor = nextCursor,
                limit = page.Limit,
                isTruncated = hasMore,
                truncatedReason = hasMore ? "result_count_limit" : null,
            },
        }, result.Freshness, new QueryPagingMetadata(page.Limit, NextCursor: nextCursor,
            TotalCount: result.Entries.Count, HasMore: hasMore));
    }
}
