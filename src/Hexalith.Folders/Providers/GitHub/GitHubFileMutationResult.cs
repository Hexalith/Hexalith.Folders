namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubFileMutationResult(
    bool IsSuccess,
    GitHubApiFailureCondition FailureCondition,
    TimeSpan? RetryAfter,
    string? TreeSha)
{
    public static GitHubFileMutationResult Success(string treeSha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(treeSha);
        return new(true, default, null, treeSha);
    }

    public static GitHubFileMutationResult Failure(
        GitHubApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, condition, retryAfter, null);

    public override string ToString() => nameof(GitHubFileMutationResult);
}
