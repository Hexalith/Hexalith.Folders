namespace Hexalith.Folders.Workers.SemanticIndexing;

internal sealed class FailClosedSemanticIndexingContentMaterializer : ISemanticIndexingContentMaterializer
{
    public ValueTask<SemanticIndexingContentMaterializationResult> MaterializeAsync(
        SemanticIndexingContentMaterializationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(SemanticIndexingContentMaterializationResult.Unavailable(
            "content_materializer_unavailable",
            retryable: true));
    }
}
