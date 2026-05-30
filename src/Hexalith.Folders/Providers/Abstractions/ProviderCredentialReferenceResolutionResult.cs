namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderCredentialReferenceResolutionResult(
    bool IsSuccess,
    string? AccessToken,
    ProviderFailureCategory FailureCategory,
    string ReasonCode,
    TimeSpan? RetryAfter)
{
    public static ProviderCredentialReferenceResolutionResult Success(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        return new(true, accessToken, ProviderFailureCategory.None, "success", null);
    }

    public static ProviderCredentialReferenceResolutionResult Failure(
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
        => new(false, null, category, reasonCode, retryAfter);
}
