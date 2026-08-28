using System.Collections;

using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

/// <summary>
/// Event list that parks the seeding writer inside the repository gate so an independent reader can be
/// observed blocking on it.
/// </summary>
/// <remarks>
/// The pause is triggered on the enumeration pass named by <see cref="BlockOnEnumeration"/>. That number
/// is load-bearing: <c>InMemoryFolderRepository.Seed</c> enumerates its argument exactly twice inside the
/// gate -- first through <c>FolderState.Apply</c>, then through its idempotency-ledger validation loop --
/// and pass 2 is the last point that still precedes every state and ledger write. If <c>Seed</c> ever
/// changes how many times it enumerates, this constant must move with it; otherwise the writer never
/// signals <see cref="WriterHoldingGate"/> and the waiting test fails on its coordination timeout.
/// </remarks>
internal sealed class CoordinatedSeedEventList(
    IReadOnlyList<IFolderEvent> events,
    TimeSpan coordinationTimeout,
    CancellationToken cancellationToken) : IReadOnlyList<IFolderEvent>
{
    internal const int BlockOnEnumeration = 2;

    private readonly CancellationToken _cancellationToken = cancellationToken;
    private readonly TimeSpan _coordinationTimeout = coordinationTimeout;
    private readonly IReadOnlyList<IFolderEvent> _events = events ?? throw new ArgumentNullException(nameof(events));
    private readonly TaskCompletionSource _releaseWriter = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _writerHoldingGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _enumerationCount;

    public int Count => _events.Count;

    public IFolderEvent this[int index] => _events[index];

    // Total enumeration passes observed. Tests assert this equals BlockOnEnumeration after Seed returns, so
    // a Seed refactor that changes the enumeration count fails on a named expectation instead of degrading
    // this harness silently (fewer passes: a coordination timeout; more passes: a park that no longer
    // precedes the state and ledger writes while the test still reports green).
    public int EnumerationCount => Volatile.Read(ref _enumerationCount);

    public Task WriterHoldingGate => _writerHoldingGate.Task;

    public IEnumerator<IFolderEvent> GetEnumerator()
    {
        if (Interlocked.Increment(ref _enumerationCount) == BlockOnEnumeration)
        {
            _writerHoldingGate.TrySetResult();
            _releaseWriter.Task
                .WaitAsync(_coordinationTimeout, _cancellationToken)
                .GetAwaiter()
                .GetResult();
        }

        return _events.GetEnumerator();
    }

    public void ReleaseWriter() => _releaseWriter.TrySetResult();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
