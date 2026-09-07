namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoCommitResult(
    bool IsSuccess,
    ForgejoApiFailureCondition FailureCondition,
    TimeSpan? RetryAfter,
    string? CommitSha,
    string? ObservedCommitSha)
{
    public static ForgejoCommitResult Success(string commitSha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
        return new(true, default, null, commitSha, commitSha);
    }

    public static ForgejoCommitResult Failure(
        ForgejoApiFailureCondition condition,
        TimeSpan? retryAfter = null,
        string? observedCommitSha = null)
        => new(false, condition, retryAfter, null, observedCommitSha);

    public override string ToString() => nameof(ForgejoCommitResult);
}
