using System.Collections.Concurrent;

namespace Hexalith.Folders.Observability;

public sealed class InMemoryFolderAuditObserver : IFolderAuditObserver
{
    private readonly ConcurrentQueue<FolderAuditObservation> _observations = new();

    public IReadOnlyList<FolderAuditObservation> Observations => [.. _observations];

    public ValueTask ObserveAsync(FolderAuditObservation observation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        _observations.Enqueue(observation);
        return ValueTask.CompletedTask;
    }
}
