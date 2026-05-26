namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubRepositoryCreationResult(
    bool IsSuccess,
    bool EquivalentExisting,
    GitHubApiFailureCondition? FailureCondition,
    TimeSpan? RetryAfter)
{
    public static GitHubRepositoryCreationResult Success(bool equivalentExisting = false)
        => new(true, equivalentExisting, null, null);

    public static GitHubRepositoryCreationResult Failure(
        GitHubApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, EquivalentExisting: false, condition, retryAfter);
}
