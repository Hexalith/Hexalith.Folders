using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubCredentialResolutionResult(
    bool IsSuccess,
    GitHubCredentialLease? Credential,
    ProviderFailureCategory FailureCategory,
    string ReasonCode,
    TimeSpan? RetryAfter)
{
    public static GitHubCredentialResolutionResult Success(GitHubCredentialLease credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        return new(true, credential, ProviderFailureCategory.None, "success", null);
    }

    public static GitHubCredentialResolutionResult Failure(
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
        => new(false, null, category, reasonCode, retryAfter);
}

