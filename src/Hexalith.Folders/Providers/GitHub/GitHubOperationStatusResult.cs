using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubOperationStatusResult(
    bool IsSuccess,
    ProviderOperationStatusKind Status,
    GitHubApiFailureCondition FailureCondition,
    TimeSpan? RetryAfter,
    string? ObservedSha,
    string? ObservedFullRef,
    string? ObservedObjectType)
{
    public static GitHubOperationStatusResult Observed(
        ProviderOperationStatusKind status,
        string observedSha,
        string observedFullRef = "refs/heads/main",
        string observedObjectType = "commit")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(observedSha);
        ArgumentException.ThrowIfNullOrWhiteSpace(observedFullRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(observedObjectType);
        return new(true, status, default, null, observedSha, observedFullRef, observedObjectType);
    }

    public static GitHubOperationStatusResult Conflicting(
        string? observedSha,
        string? observedFullRef,
        string? observedObjectType)
        => new(true, ProviderOperationStatusKind.Conflicting, default, null, observedSha, observedFullRef, observedObjectType);

    public static GitHubOperationStatusResult Failure(
        GitHubApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, ProviderOperationStatusKind.Unavailable, condition, retryAfter, null, null, null);

    public override string ToString() => nameof(GitHubOperationStatusResult);
}
