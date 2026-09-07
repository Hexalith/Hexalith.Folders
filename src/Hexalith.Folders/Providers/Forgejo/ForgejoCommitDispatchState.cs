namespace Hexalith.Folders.Providers.Forgejo;

/// <summary>
/// Shares the receive-pack dispatch boundary with the bounded asynchronous caller.
/// </summary>
internal sealed class ForgejoCommitDispatchState
{
    private const int Pending = 0;
    private const int DispatchClaimed = 1;
    private const int CallerInterrupted = 2;
    private string? _createdCommitSha;
    private int _dispatchBoundary;

    /// <summary>
    /// Gets the locally created commit identity, when it has been recorded.
    /// </summary>
    public string? CreatedCommitSha => Volatile.Read(ref _createdCommitSha);

    /// <summary>
    /// Gets whether receive-pack crossed the final pre-upload callback.
    /// </summary>
    public bool MutationDispatched => Volatile.Read(ref _dispatchBoundary) == DispatchClaimed;

    /// <summary>
    /// Records the local commit identity before the remote mutation boundary.
    /// </summary>
    /// <param name="commitSha">The metadata-safe commit object identity.</param>
    public void RecordCreatedCommit(string commitSha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
        Volatile.Write(ref _createdCommitSha, commitSha);
    }

    /// <summary>
    /// Atomically claims the final receive-pack upload boundary.
    /// </summary>
    /// <returns><see langword="true"/> when dispatch won before caller interruption.</returns>
    public bool TryClaimMutationDispatch()
        => Interlocked.CompareExchange(ref _dispatchBoundary, DispatchClaimed, Pending) == Pending;

    /// <summary>
    /// Atomically records conclusive caller interruption before receive-pack dispatch.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when interruption won or was already recorded;
    /// <see langword="false"/> when dispatch had already claimed the boundary.
    /// </returns>
    public bool TryRecordCallerInterruption()
    {
        int observed = Interlocked.CompareExchange(ref _dispatchBoundary, CallerInterrupted, Pending);
        return observed is Pending or CallerInterrupted;
    }
}
