namespace Hexalith.Folders.Providers.Abstractions;

public sealed record ProviderCapabilityDiscoveryResult(
    bool IsSuccess,
    ProviderCapabilityProfile? Profile,
    ProviderFailureCategory FailureCategory,
    string CategoryCode,
    string ReasonCode,
    string SafeRemediationCode,
    bool Retryable,
    TimeSpan? RetryAfter,
    string CorrelationId,
    ProviderCapabilityProfileVersion? ProfileVersion)
{
    public static ProviderCapabilityDiscoveryResult Success(ProviderCapabilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new(
            true,
            profile,
            ProviderFailureCategory.None,
            ProviderFailureCategory.None.ToCategoryCode(),
            "success",
            "none",
            false,
            null,
            profile.CorrelationId,
            profile.Version);
    }

    public static ProviderCapabilityDiscoveryResult Failure(
        ProviderFailureCategory category,
        string reasonCode,
        string correlationId,
        TimeSpan? retryAfter = null,
        string? safeRemediationCode = null)
    {
        string categoryCode = category.ToCategoryCode();
        return new(
            false,
            null,
            category,
            categoryCode,
            string.IsNullOrWhiteSpace(reasonCode) ? categoryCode : reasonCode,
            string.IsNullOrWhiteSpace(safeRemediationCode) ? $"{categoryCode}_remediation" : safeRemediationCode,
            category.IsRetryableByDefault(),
            retryAfter,
            string.IsNullOrWhiteSpace(correlationId) ? "correlation_unavailable" : correlationId,
            null);
    }
}
