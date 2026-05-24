namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubReadinessResult(
    bool IsSuccess,
    GitHubPermissionEvidence? Permissions,
    GitHubRateLimitEvidence? RateLimit,
    GitHubApiFailureCondition? FailureCondition,
    TimeSpan? RetryAfter)
{
    public static GitHubReadinessResult Success(
        GitHubPermissionEvidence permissions,
        GitHubRateLimitEvidence rateLimit)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(rateLimit);
        return new(true, permissions, rateLimit, null, null);
    }

    public static GitHubReadinessResult Failure(
        GitHubApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, null, null, condition, retryAfter);
}

