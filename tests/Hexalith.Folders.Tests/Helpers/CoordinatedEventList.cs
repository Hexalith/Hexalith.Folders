using System.Collections;

namespace Hexalith.Folders.Tests.Helpers;

/// <summary>Parks a writer during a selected enumeration pass so tests can observe competing reads.</summary>
internal sealed class CoordinatedEventList<T>(
    IReadOnlyList<T> events,
    int blockOnEnumeration,
    TimeSpan coordinationTimeout,
    CancellationToken cancellationToken) : IReadOnlyList<T>
{
    private readonly CancellationToken _cancellationToken = cancellationToken;
    private readonly TimeSpan _coordinationTimeout = coordinationTimeout;
    private readonly IReadOnlyList<T> _events = events ?? throw new ArgumentNullException(nameof(events));
    private readonly TaskCompletionSource<Action?> _releaseWriter = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _writerHoldingGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _enumerationCount;

    public int Count => _events.Count;

    public T this[int index] => _events[index];

    public int EnumerationCount => Volatile.Read(ref _enumerationCount);

    public Task WriterHoldingGate => _writerHoldingGate.Task;

    public IEnumerator<T> GetEnumerator()
    {
        if (Interlocked.Increment(ref _enumerationCount) == blockOnEnumeration)
        {
            _writerHoldingGate.TrySetResult();
            Action? beforeEnumeration = _releaseWriter.Task
                .WaitAsync(_coordinationTimeout, _cancellationToken)
                .GetAwaiter()
                .GetResult();
            beforeEnumeration?.Invoke();
        }

        return _events.GetEnumerator();
    }

    public void ReleaseWriter(Action? beforeEnumeration = null) => _releaseWriter.TrySetResult(beforeEnumeration);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
