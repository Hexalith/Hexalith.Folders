namespace Hexalith.Folders.Projections.SemanticIndexing;

public interface ISemanticIndexingBridgeReadModel
{
    Task<SemanticIndexingBridgeEntry?> GetFileVersionAsync(
        SemanticIndexingFileVersionIdentity identity,
        CancellationToken cancellationToken = default);
}
