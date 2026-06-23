namespace Hexalith.Folders.Workers.SemanticIndexing;

public interface ISemanticIndexingPort
{
    /// <summary>
    /// Publishes a curated <c>SearchIndexEntryChanged</c> upsert (live indexing, <c>folders.status = active</c>) for a
    /// file version to the Memories search index.
    /// </summary>
    ValueTask<SemanticIndexingResult> IndexFileVersionAsync(
        SemanticIndexingRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Hard-deletes a previously-indexed file version by publishing a <c>SearchIndexEntryRemoved</c> CloudEvent
    /// (Memories <c>KeyDeleteAsync</c> drops the document; re-delivery is an idempotent no-op).
    /// </summary>
    ValueTask<SemanticIndexingResult> RemoveFileVersionAsync(
        SemanticIndexingRemovalRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Soft-deletes a previously-indexed file version by re-publishing the full <c>SearchIndexEntryChanged</c> document
    /// with <c>folders.status = archived</c> (the document stays searchable but filterable). The full document is
    /// re-sent because the Memories upsert is a destructive full-field overwrite (Story 10.4 decision (A)).
    /// </summary>
    ValueTask<SemanticIndexingResult> SoftDeleteFileVersionAsync(
        SemanticIndexingArchiveRequest request,
        CancellationToken cancellationToken);
}
