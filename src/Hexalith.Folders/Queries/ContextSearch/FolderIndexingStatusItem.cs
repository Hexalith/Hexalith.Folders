namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// A metadata-only indexing-status entry for one indexed file version, for the read-only console projection.
/// Carries no content, snippet, raw path, source URI, or memory-unit id — only an opaque handle, the status
/// vocabulary, a classification reason code, and visibly-distinct sensitivity/redaction markers.
/// </summary>
/// <param name="FileVersionReference">The opaque file-version handle (e.g. <c>fv-…</c>).</param>
/// <param name="IndexingStatus">The status code: indexed/stale/skipped/failed/tombstoned/reconciliation_required/unknown.</param>
/// <param name="ReasonCode">The metadata-safe reason classification (e.g. <c>memories_accepted</c>, <c>content_too_large</c>).</param>
/// <param name="Sensitivity">The sensitivity tier of the metadata.</param>
/// <param name="Redaction">The redaction marker, visibly distinct from unknown/missing.</param>
public sealed record FolderIndexingStatusItem(
    string FileVersionReference,
    string IndexingStatus,
    string ReasonCode,
    string Sensitivity,
    string Redaction);
