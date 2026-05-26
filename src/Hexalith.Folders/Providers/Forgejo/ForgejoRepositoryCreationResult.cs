namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoRepositoryCreationResult(
    bool IsSuccess,
    bool EquivalentExisting,
    ForgejoApiFailureCondition? FailureCondition,
    TimeSpan? RetryAfter)
{
    public static ForgejoRepositoryCreationResult Success(bool equivalentExisting = false)
        => new(true, equivalentExisting, null, null);

    public static ForgejoRepositoryCreationResult Failure(
        ForgejoApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, EquivalentExisting: false, condition, retryAfter);
}
