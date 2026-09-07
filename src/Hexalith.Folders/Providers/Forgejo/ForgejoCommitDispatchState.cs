namespace Hexalith.Folders.Providers.Forgejo;

/// <summary>
/// Shares the receive-pack dispatch boundary with the bounded asynchronous caller.
/// </summary>
internal sealed class ForgejoCommitDispatchState
{
    private string? _createdCommitSha;
    private int _mutationDispatched;

    /// <summary>
    /// Gets the locally created commit identity, when it has been recorded.
    /// </summary>
    public string? CreatedCommitSha => Volatile.Read(ref _createdCommitSha);

    /// <summary>
    /// Gets whether receive-pack crossed the final pre-upload callback.
    /// </summary>
    public bool MutationDispatched => Volatile.Read(ref _mutationDispatched) != 0;

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
    /// Marks the final receive-pack upload boundary.
    /// </summary>
    public void MarkMutationDispatched() => Interlocked.Exchange(ref _mutationDispatched, 1);
}
