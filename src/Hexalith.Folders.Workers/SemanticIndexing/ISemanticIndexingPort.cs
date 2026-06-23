namespace Hexalith.Folders.Workers.SemanticIndexing;

public interface ISemanticIndexingPort
{
    ValueTask<SemanticIndexingResult> IndexFileVersionAsync(
        SemanticIndexingRequest request,
        CancellationToken cancellationToken);
}
