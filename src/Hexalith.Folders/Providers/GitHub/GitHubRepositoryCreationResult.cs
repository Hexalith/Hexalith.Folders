namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubRepositoryCreationResult(
    bool IsSuccess,
    bool EquivalentExisting,
    GitHubApiFailureCondition? FailureCondition,
    TimeSpan? RetryAfter,
    string? CanonicalRepositoryId)
{
    public static GitHubRepositoryCreationResult Success(
        bool equivalentExisting = false,
        string? canonicalRepositoryId = null)
        => new(true, equivalentExisting, null, null, canonicalRepositoryId);

    public static GitHubRepositoryCreationResult Failure(
        GitHubApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, EquivalentExisting: false, condition, retryAfter, CanonicalRepositoryId: null);
}
