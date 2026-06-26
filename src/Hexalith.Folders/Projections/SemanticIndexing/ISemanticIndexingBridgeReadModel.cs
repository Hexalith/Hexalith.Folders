namespace Hexalith.Folders.Projections.SemanticIndexing;

public interface ISemanticIndexingBridgeReadModel
{
    bool IsAvailable { get; }

    Task<SemanticIndexingBridgeEntry?> GetFileVersionAsync(
        SemanticIndexingFileVersionIdentity identity,
        CancellationToken cancellationToken = default);

    Task<SemanticIndexingBridgeEntry?> GetFileVersionByIdAsync(
        string managedTenantId,
        string folderId,
        string fileVersionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every bridge entry for a single tenant-scoped folder, ordered by read-model key. Used by the
    /// Story 10.5 query facade to hydrate search hits from the authoritative Folders read (the search index is
    /// non-authoritative) and by the indexing-status console projection. The result is tenant-prefixed by
    /// construction: only entries whose identity matches <paramref name="managedTenantId"/> and
    /// <paramref name="folderId"/> are returned, so no caller can read another tenant's or folder's entries.
    /// </summary>
    /// <param name="managedTenantId">The authoritative managed tenant identifier.</param>
    /// <param name="folderId">The folder identifier whose indexed file versions are requested.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The folder's bridge entries (possibly empty); never null.</returns>
    Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ListFolderAsync(
        string managedTenantId,
        string folderId,
        CancellationToken cancellationToken = default);
}
