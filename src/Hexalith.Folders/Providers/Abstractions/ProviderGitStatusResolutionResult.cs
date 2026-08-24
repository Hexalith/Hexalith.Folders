namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderGitStatusResolutionResult(
    bool IsSuccess,
    ProviderGitStatusResolvedInput? Input,
    ProviderFailureCategory FailureCategory,
    string ReasonCode,
    TimeSpan? RetryAfter)
{
    public static ProviderGitStatusResolutionResult Success(ProviderGitStatusResolvedInput input)
        => new(true, input ?? throw new ArgumentNullException(nameof(input)), ProviderFailureCategory.None, "success", null);

    public static ProviderGitStatusResolutionResult Failure(
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
        => new(false, null, category, reasonCode, retryAfter);
}
