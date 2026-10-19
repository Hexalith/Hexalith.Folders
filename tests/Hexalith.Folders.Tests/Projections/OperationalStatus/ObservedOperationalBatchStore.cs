using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

internal sealed class ObservedOperationalBatchStore(IReadModelBatchStore inner) : IReadModelBatchStore
{
    public List<ReadModelBatch> Batches { get; } = [];

    public Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
    {
        Batches.Add(batch);
        return inner.ExecuteAsync(batch, cancellationToken);
    }
}
