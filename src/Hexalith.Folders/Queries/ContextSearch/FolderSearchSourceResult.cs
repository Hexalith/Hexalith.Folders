namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// The Memories-free result of a search-source call. <see cref="Hits"/> are in the source's relevance order; the
/// handler trims, hydrates, and redacts them. <see cref="TotalCount"/> is the index's total match count, while
/// <see cref="RawCount"/> is the raw row count returned by the source before malformed rows are dropped.
/// </summary>
/// <param name="Status">The availability outcome.</param>
/// <param name="Hits">The Memories-free hits in relevance order.</param>
/// <param name="TotalCount">The total number of matching documents in the index.</param>
/// <param name="RawCount">The number of raw source rows returned for this page before recovery/trimming.</param>
public sealed record FolderSearchSourceResult(
    FolderSearchSourceStatus Status,
    IReadOnlyList<FolderSearchSourceHit> Hits,
    long TotalCount,
    int RawCount)
{
    /// <summary>A safe, fail-closed result reporting the search index is unavailable.</summary>
    public static FolderSearchSourceResult Unavailable()
        => new(FolderSearchSourceStatus.Unavailable, [], 0, 0);
}
