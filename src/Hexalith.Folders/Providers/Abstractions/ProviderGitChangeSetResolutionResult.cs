namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderGitChangeSetResolutionResult(
    bool IsSuccess,
    ProviderGitChangeSetResolvedInput? Input,
    ProviderFailureCategory FailureCategory,
    string ReasonCode,
    TimeSpan? RetryAfter)
{
    public static ProviderGitChangeSetResolutionResult Success(ProviderGitChangeSetResolvedInput input)
        => new(true, input ?? throw new ArgumentNullException(nameof(input)), ProviderFailureCategory.None, "success", null);

    public static ProviderGitChangeSetResolutionResult Failure(
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
        => new(false, null, category, reasonCode, retryAfter);
}
