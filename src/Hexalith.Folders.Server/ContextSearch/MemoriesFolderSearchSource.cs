using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Queries.ContextSearch;

using Hexalith.Memories.Client.Rest;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MemoriesScoredResult = Hexalith.Memories.Contracts.V1.ScoredResult;
using MemoriesSearchResult = Hexalith.Memories.Contracts.V1.SearchResult;

namespace Hexalith.Folders.Server.ContextSearch;

/// <summary>
/// The Story 10.5 Option-B search source: the single approved Folders.Server bridge to the Memories search index.
/// It calls the typed <see cref="MemoriesClient.SearchAsync"/> over the shared <c>folders-index</c> tenant,
/// constrains the query with the authoritative <c>(managedTenantId, organizationId, folderId)</c> + <c>status=active</c>
/// attribute filters, recovers each hit's identity from <c>ScoredResult.SourceUri</c> ONLY (the response carries no
/// attributes), DROPS the content snippet, and degrades to a safe unavailable result on any remote/transport failure.
/// It performs no authorization or hydration — those are the handler's authoritative responsibilities. Mirrors
/// <c>Hexalith.Tenants.UI.Services.Gateways.TenantQueryGateway</c>. It NEVER calls the [Experimental] RAG IngestAsync.
/// </summary>
internal sealed class MemoriesFolderSearchSource(
    MemoriesClient memoriesClient,
    ILogger<MemoriesFolderSearchSource>? logger = null) : IFolderSearchSource
{
    private const string SourceUriScheme = "folders://";
    private const string OrganizationsMarker = "organizations";
    private const string FoldersMarker = "folders";
    private const string WorkspacesMarker = "workspaces";
    private const string FileVersionsMarker = "file-versions";

    private readonly MemoriesClient _memoriesClient = memoriesClient;
    private readonly ILogger<MemoriesFolderSearchSource> _logger =
        logger ?? NullLogger<MemoriesFolderSearchSource>.Instance;

    public async Task<FolderSearchSourceResult> SearchAsync(
        FolderSearchSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        MemoriesSearchResult searchResult;
        try
        {
            searchResult = await _memoriesClient
                .SearchAsync(
                    new SearchRequest(
                        TenantId: FoldersSemanticIndexingAttributes.IndexTenant,
                        Axis: FoldersSemanticIndexingAttributes.SearchAxis,
                        Query: request.QueryText,
                        MaxResults: request.Limit,
                        Offset: request.Offset,
                        AttributeFilters: BuildAttributeFilters(request)),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (IsMemoriesUnavailable(ex, cancellationToken))
        {
            // Never let a remote/transport failure reach the handler's circuit — degrade to a safe unavailable result.
            _logger.LogWarning(
                ex,
                "Memories search index unavailable; degrading the context-search facade. Exception type: {ExceptionType}",
                ex.GetType().FullName);
            return FolderSearchSourceResult.Unavailable();
        }

        // In-band degradation (syntactic axis down / partial) is reported safely, not as content.
        if (searchResult.Degraded
            || searchResult.UnavailableAxes?.Any(static axis =>
                string.Equals(axis, FoldersSemanticIndexingAttributes.SearchAxis, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return new FolderSearchSourceResult(FolderSearchSourceStatus.Degraded, [], searchResult.TotalCount);
        }

        List<FolderSearchSourceHit> hits = [];
        foreach (MemoriesScoredResult scored in searchResult.Results)
        {
            // Recover identity from SourceUri ONLY (never the dropped ContentSnippet or the MemoryUnitId); malformed
            // entries are skipped. The handler re-checks every component against the authorized scope.
            if (TryRecoverHit(scored, out FolderSearchSourceHit? hit))
            {
                hits.Add(hit);
            }
        }

        return new FolderSearchSourceResult(FolderSearchSourceStatus.Available, hits, searchResult.TotalCount);
    }

    private static IReadOnlyDictionary<string, string> BuildAttributeFilters(FolderSearchSourceRequest request)
    {
        Dictionary<string, string> filters = new(StringComparer.Ordinal)
        {
            [FoldersSemanticIndexingAttributes.ManagedTenantIdAttribute] = request.ManagedTenantId,
            [FoldersSemanticIndexingAttributes.FolderIdAttribute] = request.FolderId,
            // Exclude archived (soft-deleted) units without re-evaluating folder state (Story 10.4 enabler).
            [FoldersSemanticIndexingAttributes.StatusAttribute] = FoldersSemanticIndexingAttributes.StatusActive,
        };

        if (request.OrganizationId.Length > 0)
        {
            filters[FoldersSemanticIndexingAttributes.OrganizationIdAttribute] = request.OrganizationId;
        }

        return filters;
    }

    private static bool TryRecoverHit(MemoriesScoredResult scored, out FolderSearchSourceHit hit)
    {
        hit = default!;
        string sourceUri = scored.SourceUri;
        if (string.IsNullOrWhiteSpace(sourceUri)
            || !sourceUri.StartsWith(SourceUriScheme, StringComparison.Ordinal))
        {
            return false;
        }

        // Shape: folders://{tenant}/organizations/{org}/folders/{folder}/workspaces/{ws}/file-versions/{fv}.
        // Parse by explicit segments rather than Uri authority (tenant ids may contain ':' which Uri reads as a port).
        string[] parts = sourceUri[SourceUriScheme.Length..].Split('/');
        if (parts.Length != 9
            || !string.Equals(parts[1], OrganizationsMarker, StringComparison.Ordinal)
            || !string.Equals(parts[3], FoldersMarker, StringComparison.Ordinal)
            || !string.Equals(parts[5], WorkspacesMarker, StringComparison.Ordinal)
            || !string.Equals(parts[7], FileVersionsMarker, StringComparison.Ordinal)
            || parts.Any(static part => part.Length == 0))
        {
            return false;
        }

        hit = new FolderSearchSourceHit(
            parts[0],
            parts[2],
            parts[4],
            parts[6],
            parts[8],
            scored.Score);
        return true;
    }

    private static bool IsMemoriesUnavailable(Exception exception, CancellationToken cancellationToken)
        => exception switch
        {
            MemoriesRemoteException => true,
            HttpRequestException => true,
            // MemoriesClient with no configured base address (Memories:BaseAddress unset) throws this.
            InvalidOperationException => true,
            // A timeout from the client (not the caller cancelling the operation).
            TaskCanceledException or OperationCanceledException => !cancellationToken.IsCancellationRequested,
            _ => false,
        };
}
