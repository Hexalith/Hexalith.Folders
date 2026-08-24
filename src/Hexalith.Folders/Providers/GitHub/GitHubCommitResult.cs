namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubCommitResult(
    bool IsSuccess,
    GitHubApiFailureCondition? FailureCondition,
    TimeSpan? RetryAfter,
    string? CommitSha)
{
    public static GitHubCommitResult Success(string commitSha)
        => new(true, null, null, commitSha);

    public static GitHubCommitResult Failure(
        GitHubApiFailureCondition condition,
        TimeSpan? retryAfter = null,
        string? commitSha = null)
        => new(false, condition, retryAfter, commitSha);
}
