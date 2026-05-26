using System.Collections.Concurrent;

namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed class InMemoryProviderReadinessEvidenceStore : IProviderReadinessEvidenceStore
{
    private readonly ConcurrentQueue<ProviderReadinessEvidenceRecord> _records = new();

    public IReadOnlyList<ProviderReadinessEvidenceRecord> Records => _records.ToArray();

    public Task StoreAsync(ProviderReadinessEvidenceRecord evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        cancellationToken.ThrowIfCancellationRequested();

        _records.Enqueue(evidence);
        return Task.CompletedTask;
    }
}
