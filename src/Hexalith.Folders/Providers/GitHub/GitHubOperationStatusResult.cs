using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubOperationStatusResult(
    bool IsSuccess,
    ProviderOperationStatusKind Status,
    GitHubApiFailureCondition FailureCondition,
    TimeSpan? RetryAfter,
    string? ObservedSha)
{
    public static GitHubOperationStatusResult Observed(ProviderOperationStatusKind status, string observedSha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(observedSha);
        return new(true, status, default, null, observedSha);
    }

    public static GitHubOperationStatusResult Failure(
        GitHubApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, ProviderOperationStatusKind.Unavailable, condition, retryAfter, null);

    public override string ToString() => nameof(GitHubOperationStatusResult);
}
