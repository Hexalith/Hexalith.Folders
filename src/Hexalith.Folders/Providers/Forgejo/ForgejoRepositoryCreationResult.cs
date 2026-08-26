namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoRepositoryCreationResult(
    bool IsSuccess,
    bool EquivalentExisting,
    ForgejoApiFailureCondition? FailureCondition,
    TimeSpan? RetryAfter,
    string? CanonicalRepositoryId)
{
    public static ForgejoRepositoryCreationResult Success(
        bool equivalentExisting = false,
        string? canonicalRepositoryId = null)
        => new(true, equivalentExisting, null, null, canonicalRepositoryId);

    public static ForgejoRepositoryCreationResult Failure(
        ForgejoApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, EquivalentExisting: false, condition, retryAfter, CanonicalRepositoryId: null);
}
