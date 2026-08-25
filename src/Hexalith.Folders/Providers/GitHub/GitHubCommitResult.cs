namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubCommitResult(
    bool IsSuccess,
    GitHubApiFailureCondition FailureCondition,
    TimeSpan? RetryAfter,
    string? CommitSha,
    string? CreatedCommitSha)
{
    public static GitHubCommitResult Success(string commitSha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
        return new(true, default, null, commitSha, commitSha);
    }

    public static GitHubCommitResult Failure(
        GitHubApiFailureCondition condition,
        TimeSpan? retryAfter = null,
        string? createdCommitSha = null)
        => new(false, condition, retryAfter, null, createdCommitSha);

    public override string ToString() => nameof(GitHubCommitResult);
}
