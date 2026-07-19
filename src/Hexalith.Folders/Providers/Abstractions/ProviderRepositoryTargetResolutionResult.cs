namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderRepositoryTargetResolutionResult(
    bool IsSuccess,
    ProviderRepositoryResolvedTarget? Target,
    ProviderFailureCategory FailureCategory,
    string ReasonCode,
    TimeSpan? RetryAfter)
{
    public static ProviderRepositoryTargetResolutionResult Success(ProviderRepositoryResolvedTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return new(true, target, ProviderFailureCategory.None, "success", null);
    }

    public static ProviderRepositoryTargetResolutionResult Failure(
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
    {
        string categoryCode = category.ToCategoryCode();
        return new(
            false,
            null,
            category,
            string.IsNullOrWhiteSpace(reasonCode) ? categoryCode : reasonCode,
            retryAfter);
    }
}
