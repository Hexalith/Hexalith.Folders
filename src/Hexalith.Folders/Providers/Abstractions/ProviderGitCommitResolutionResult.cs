namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderGitCommitResolutionResult(
    bool IsSuccess,
    ProviderGitCommitResolvedInput? Input,
    ProviderFailureCategory FailureCategory,
    string ReasonCode,
    TimeSpan? RetryAfter)
{
    public static ProviderGitCommitResolutionResult Success(ProviderGitCommitResolvedInput input)
        => new(true, input ?? throw new ArgumentNullException(nameof(input)), ProviderFailureCategory.None, "success", null);

    public static ProviderGitCommitResolutionResult Failure(
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
        => new(false, null, category, reasonCode, retryAfter);
}
