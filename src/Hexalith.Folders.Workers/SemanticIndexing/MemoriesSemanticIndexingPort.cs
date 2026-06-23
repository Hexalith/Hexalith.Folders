using Hexalith.Memories.Client.Rest;

namespace Hexalith.Folders.Workers.SemanticIndexing;

internal sealed class MemoriesSemanticIndexingPort : ISemanticIndexingPort
{
    private const string AdapterShellReasonCode = "adapter_shell_not_producing";

    public MemoriesSemanticIndexingPort(MemoriesClient memoriesClient)
    {
        ArgumentNullException.ThrowIfNull(memoriesClient);
    }

    public ValueTask<SemanticIndexingResult> IndexFileVersionAsync(
        SemanticIndexingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(new SemanticIndexingResult(
            SemanticIndexingStatus.Deferred,
            AdapterShellReasonCode,
            retryable: true));
    }
}
