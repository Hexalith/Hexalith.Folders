namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoRepositoryBindingResult(
    bool IsSuccess,
    bool EquivalentExisting,
    ForgejoApiFailureCondition? FailureCondition,
    TimeSpan? RetryAfter)
{
    public static ForgejoRepositoryBindingResult Success(bool equivalentExisting = false)
        => new(true, equivalentExisting, null, null);

    public static ForgejoRepositoryBindingResult Failure(
        ForgejoApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, EquivalentExisting: false, condition, retryAfter);
}
