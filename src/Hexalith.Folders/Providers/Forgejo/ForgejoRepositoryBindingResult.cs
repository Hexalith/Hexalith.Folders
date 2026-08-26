namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoRepositoryBindingResult(
    bool IsSuccess,
    bool EquivalentExisting,
    ForgejoApiFailureCondition? FailureCondition,
    TimeSpan? RetryAfter,
    string? CanonicalRepositoryId)
{
    public static ForgejoRepositoryBindingResult Success(
        bool equivalentExisting = false,
        string? canonicalRepositoryId = null)
        => new(true, equivalentExisting, null, null, canonicalRepositoryId);

    public static ForgejoRepositoryBindingResult Failure(
        ForgejoApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, EquivalentExisting: false, condition, retryAfter, CanonicalRepositoryId: null);
}
