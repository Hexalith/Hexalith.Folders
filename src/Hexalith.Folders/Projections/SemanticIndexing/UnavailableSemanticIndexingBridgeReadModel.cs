namespace Hexalith.Folders.Projections.SemanticIndexing;

/// <summary>
/// Fail-safe default <see cref="ISemanticIndexingBridgeReadModel"/> for hosts (notably the Server query facade)
/// that have no live bridge store wired. It reports no indexed file versions, so the Story 10.5 facade hydration
/// drops every search hit and the indexing-status projection renders an empty/unavailable state rather than
/// fabricating results. The live EventStore-backed read model overrides this when the bridge state store is
/// available (mirrors the <c>UnavailableWorkspaceFileContextSource</c> default-then-override pattern).
/// </summary>
public sealed class UnavailableSemanticIndexingBridgeReadModel : ISemanticIndexingBridgeReadModel
{
    public bool IsAvailable => false;

    public Task<SemanticIndexingBridgeEntry?> GetFileVersionAsync(
        SemanticIndexingFileVersionIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<SemanticIndexingBridgeEntry?>(null);
    }

    public Task<SemanticIndexingBridgeEntry?> GetFileVersionByIdAsync(
        string managedTenantId,
        string folderId,
        string fileVersionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileVersionId);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<SemanticIndexingBridgeEntry?>(null);
    }

    public Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ListFolderAsync(
        string managedTenantId,
        string folderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IReadOnlyList<SemanticIndexingBridgeEntry>>([]);
    }
}
