namespace Hexalith.Folders.Workers.SemanticIndexing;

/// <summary>
/// A worker-owned request to hard-delete a previously-indexed file version from the Memories search index by
/// publishing a <c>SearchIndexEntryRemoved</c> CloudEvent. The identity fields are reconstructed from the tombstoned
/// bridge entry's preserved <c>Identity</c> (Story 10.4 decision (C)) — never from the remove event, which carries no
/// file-version id. <see cref="IndexedEventId"/> is the stable source URI the original upsert used as its
/// <c>cloudevent.id</c>, so the removal targets exactly the upserted document under the (TenantId, AggregateId) key.
/// </summary>
public sealed record SemanticIndexingRemovalRequest
{
    public SemanticIndexingRemovalRequest(
        string managedTenantId,
        string organizationId,
        string folderId,
        string fileVersionId,
        string indexedEventId,
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
        CorrelationId = correlationId;
        TaskId = taskId;
    }

    public string ManagedTenantId { get; init; }

    public string OrganizationId { get; init; }

    public string FolderId { get; init; }

    public string FileVersionId { get; init; }

    /// <summary>
    /// Gets the stable source URI the original <c>SearchIndexEntryChanged</c> upsert used as its <c>cloudevent.id</c>
    /// (recorded as the bridge entry's <c>PublishedEventId</c>). Reused verbatim as the removal's <c>cloudevent.id</c>
    /// so the delete is byte-identical to the upsert under the composite (TenantId, AggregateId) key (Story 10.4 AC5).
    /// </summary>
    public string IndexedEventId { get; init; }

    public string CorrelationId { get; init; }

    public string TaskId { get; init; }
}
