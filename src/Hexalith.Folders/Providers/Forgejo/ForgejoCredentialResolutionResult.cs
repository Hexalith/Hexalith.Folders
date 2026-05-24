using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoCredentialResolutionResult(
    bool IsSuccess,
    ForgejoCredentialLease? Credential,
    ProviderFailureCategory FailureCategory,
    string ReasonCode,
    TimeSpan? RetryAfter)
{
    public static ForgejoCredentialResolutionResult Success(ForgejoCredentialLease credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        return new(true, credential, ProviderFailureCategory.None, "success", null);
    }

    public static ForgejoCredentialResolutionResult Failure(
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
        => new(false, null, category, reasonCode, retryAfter);
}
