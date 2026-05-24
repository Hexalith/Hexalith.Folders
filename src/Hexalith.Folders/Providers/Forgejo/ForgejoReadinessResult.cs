namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoReadinessResult(
    bool IsSuccess,
    ForgejoVersionEvidence? Version,
    ForgejoPermissionEvidence? Permissions,
    ForgejoRateLimitEvidence? RateLimit,
    ForgejoApiFailureCondition? FailureCondition,
    TimeSpan? RetryAfter)
{
    public static ForgejoReadinessResult Success(
        ForgejoVersionEvidence version,
        ForgejoPermissionEvidence permissions,
        ForgejoRateLimitEvidence rateLimit)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(rateLimit);
        return new(true, version, permissions, rateLimit, null, null);
    }

    public static ForgejoReadinessResult Failure(
        ForgejoApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(false, null, null, null, condition, retryAfter);
}
