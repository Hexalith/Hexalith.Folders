namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// The Memories-free result of a search-source call. <see cref="Hits"/> are in the source's relevance order; the
/// handler trims, hydrates, and redacts them. <see cref="TotalCount"/> is the index's total match count (used only
/// to compute the truncation flag / next cursor — never surfaced as content).
/// </summary>
/// <param name="Status">The availability outcome.</param>
/// <param name="Hits">The Memories-free hits in relevance order.</param>
/// <param name="TotalCount">The total number of matching documents in the index.</param>
public sealed record FolderSearchSourceResult(
    FolderSearchSourceStatus Status,
    IReadOnlyList<FolderSearchSourceHit> Hits,
    long TotalCount)
{
    /// <summary>A safe, fail-closed result reporting the search index is unavailable.</summary>
    public static FolderSearchSourceResult Unavailable()
        => new(FolderSearchSourceStatus.Unavailable, [], 0);
}
