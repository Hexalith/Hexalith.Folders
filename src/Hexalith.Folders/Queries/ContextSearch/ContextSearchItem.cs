namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// A single metadata-only search hit. It carries NO raw path, content snippet, matched-line text, preview, diff,
/// provider payload, or raw search text. <see cref="FileVersionReference"/> is the opaque, non-disclosing
/// file-version handle (it does not encode a filesystem path); identity-bearing values such as the Memories
/// <c>SourceUri</c>/<c>MemoryUnitId</c> are never surfaced.
/// </summary>
/// <param name="FileVersionReference">The opaque file-version handle (e.g. <c>fv-…</c>).</param>
/// <param name="IndexingStatus">The authoritative bridge indexing status code (e.g. <c>indexed</c>, <c>stale</c>).</param>
/// <param name="Sensitivity">The sensitivity tier of the metadata (e.g. <c>tenant_sensitive</c>).</param>
/// <param name="Redaction">The redaction marker, visibly distinct from unknown/missing (e.g. <c>not_redacted</c>).</param>
/// <param name="Score">The relevance score from the search axis (BM25); ordering signal only.</param>
public sealed record ContextSearchItem(
    string FileVersionReference,
    string IndexingStatus,
    string Sensitivity,
    string Redaction,
    double Score);
