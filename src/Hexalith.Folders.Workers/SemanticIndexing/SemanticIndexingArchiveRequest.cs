namespace Hexalith.Folders.Workers.SemanticIndexing;

/// <summary>
/// A worker-owned request to soft-delete a previously-indexed file version by re-publishing a
/// <c>SearchIndexEntryChanged</c> with <c>folders.status = archived</c>. Because the Memories upsert is a destructive
/// full-field overwrite (Story 10.4 decision (A)), the worker MUST re-send the complete document: the original curated
/// <see cref="IndexedText"/> and <see cref="IndexedAttributes"/> (from the tombstoned entry's preserved index-time
/// evidence) with only <c>folders.status</c> flipped to archived. When the preserved evidence does not retain the
/// index-time text/attributes (legacy entries), the port falls back to a C9-safe descriptor form and the original rich
/// searchable text is lost — an accepted, documented consequence (archived units are filtered out by Story 10.5).
/// </summary>
public sealed record SemanticIndexingArchiveRequest
{
    public SemanticIndexingArchiveRequest(
        string managedTenantId,
        string organizationId,
        string folderId,
        string fileVersionId,
        string indexedEventId,
        string? indexedText,
        IReadOnlyDictionary<string, string>? indexedAttributes,
        string correlationId,
        string taskId)
    {
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(managedTenantId);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(organizationId);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(folderId);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(fileVersionId);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(indexedEventId);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(correlationId);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(taskId);

        ManagedTenantId = managedTenantId;
        OrganizationId = organizationId;
        FolderId = folderId;
        FileVersionId = fileVersionId;
        IndexedEventId = indexedEventId;
        IndexedText = indexedText;
        IndexedAttributes = indexedAttributes;
        CorrelationId = correlationId;
        TaskId = taskId;
    }

    public string ManagedTenantId { get; init; }

    public string OrganizationId { get; init; }

    public string FolderId { get; init; }

    public string FileVersionId { get; init; }

    /// <summary>
    /// Gets the stable source URI the original upsert used as its <c>cloudevent.id</c>. Reused verbatim so the
    /// archive re-send overwrites exactly the upserted document under the composite (TenantId, AggregateId) key.
    /// </summary>
    public string IndexedEventId { get; init; }

    /// <summary>Gets the original curated index-time text to re-send; null falls back to a C9-safe descriptor form.</summary>
    public string? IndexedText { get; init; }

    /// <summary>Gets the original index-time attribute set to re-send (with <c>folders.status</c> overwritten to archived).</summary>
    public IReadOnlyDictionary<string, string>? IndexedAttributes { get; init; }

    public string CorrelationId { get; init; }

    public string TaskId { get; init; }
}
