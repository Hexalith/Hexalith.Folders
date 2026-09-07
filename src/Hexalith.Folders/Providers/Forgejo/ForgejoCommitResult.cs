namespace Hexalith.Folders.Providers.Forgejo;

/// <summary>
/// Carries a metadata-only smart-HTTP commit outcome inside the Forgejo adapter.
/// </summary>
/// <param name="IsSuccess">Whether the requested ref update was confirmed.</param>
/// <param name="FailureCondition">The bounded failure condition.</param>
/// <param name="RetryAfter">The safe retry delay, when applicable.</param>
/// <param name="CommitSha">The confirmed commit identity retained inside the adapter boundary.</param>
/// <param name="ObservedCommitSha">The created commit identity retained for durable evidence.</param>
/// <param name="MutationDispatched">Whether receive-pack crossed the dispatch boundary.</param>
internal sealed record ForgejoCommitResult(
    bool IsSuccess,
    ForgejoApiFailureCondition FailureCondition,
    TimeSpan? RetryAfter,
    string? CommitSha,
    string? ObservedCommitSha,
    bool MutationDispatched)
{
    public static ForgejoCommitResult Success(string commitSha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
        return new(true, default, null, commitSha, commitSha, true);
    }

    public static ForgejoCommitResult Failure(
        ForgejoApiFailureCondition condition,
        TimeSpan? retryAfter = null,
        string? observedCommitSha = null,
        bool mutationDispatched = false)
        => new(false, condition, retryAfter, null, observedCommitSha, mutationDispatched);

    public override string ToString() => nameof(ForgejoCommitResult);
}
