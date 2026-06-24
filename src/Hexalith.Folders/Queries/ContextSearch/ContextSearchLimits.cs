namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// C4 query-bound metadata for a context search. Mirrors the file-context limit shape: raw search text is never
/// included — only the query family, configured limit, result count, byte budget, elapsed time, and truncation.
/// </summary>
/// <param name="QueryFamily">The query family (constant <c>semantic_reference_pending</c> for this facade).</param>
/// <param name="ConfiguredLimit">The effective C4 result limit applied.</param>
/// <param name="ActualCount">The number of items returned after authorization, trimming, and hydration.</param>
/// <param name="ActualBytes">The approximate aggregate response size in bytes.</param>
/// <param name="ElapsedMilliseconds">Server-side elapsed time.</param>
/// <param name="IsTruncated">Whether more results exist beyond this page.</param>
/// <param name="TruncatedReason">The truncation reason, or <c>not_truncated</c>.</param>
public sealed record ContextSearchLimits(
    string QueryFamily,
    int ConfiguredLimit,
    int ActualCount,
    long ActualBytes,
    long ElapsedMilliseconds,
    bool IsTruncated,
    string TruncatedReason);
