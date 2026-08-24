namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubFileChangeSetResult(
    bool IsSuccess,
    GitHubApiFailureCondition? FailureCondition,
    TimeSpan? RetryAfter,
    string? StagedTreeSha)
{
    public static GitHubFileChangeSetResult Success(string stagedTreeSha)
        => new(true, null, null, stagedTreeSha);

    public static GitHubFileChangeSetResult Failure(
        GitHubApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, condition, retryAfter, null);
}
