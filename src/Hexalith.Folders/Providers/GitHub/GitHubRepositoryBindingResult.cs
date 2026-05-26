namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubRepositoryBindingResult(
    bool IsSuccess,
    bool EquivalentExisting,
    GitHubApiFailureCondition? FailureCondition,
    TimeSpan? RetryAfter)
{
    public static GitHubRepositoryBindingResult Success(bool equivalentExisting = false)
        => new(true, equivalentExisting, null, null);

    public static GitHubRepositoryBindingResult Failure(
        GitHubApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, EquivalentExisting: false, condition, retryAfter);
}
