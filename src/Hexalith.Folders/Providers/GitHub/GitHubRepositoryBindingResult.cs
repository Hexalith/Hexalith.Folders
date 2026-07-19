namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubRepositoryBindingResult(
    bool IsSuccess,
    bool EquivalentExisting,
    GitHubApiFailureCondition? FailureCondition,
    TimeSpan? RetryAfter,
    string? CanonicalRepositoryId)
{
    public static GitHubRepositoryBindingResult Success(
        bool equivalentExisting = false,
        string? canonicalRepositoryId = null)
        => new(true, equivalentExisting, null, null, canonicalRepositoryId);

    public static GitHubRepositoryBindingResult Failure(
        GitHubApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, EquivalentExisting: false, condition, retryAfter, CanonicalRepositoryId: null);
}
