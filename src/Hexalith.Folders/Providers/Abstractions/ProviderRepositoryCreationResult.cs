namespace Hexalith.Folders.Providers.Abstractions;

public sealed record ProviderRepositoryCreationResult(
    bool IsSuccess,
    bool EquivalentExisting,
    ProviderFailureCategory FailureCategory,
    string CategoryCode,
    string ReasonCode,
    string SafeRemediationCode,
    bool Retryable,
    TimeSpan? RetryAfter,
    string RepositoryBindingId,
    string ProviderBindingRef,
    string CorrelationId,
    string? SafeTargetFingerprint)
{
    public static ProviderRepositoryCreationResult Success(
        ProviderRepositoryCreationRequest request,
        bool equivalentExisting,
        string safeTargetFingerprint)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new(
            true,
            equivalentExisting,
            ProviderFailureCategory.None,
            ProviderFailureCategory.None.ToCategoryCode(),
            equivalentExisting ? "existing_equivalent" : "success",
            "none",
            Retryable: false,
            RetryAfter: null,
            request.RepositoryBindingId,
            request.ProviderBindingRef,
            request.CorrelationId,
            safeTargetFingerprint);
    }

    public static ProviderRepositoryCreationResult Failure(
        ProviderRepositoryCreationRequest request,
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null,
        string? safeRemediationCode = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        string categoryCode = category.ToCategoryCode();
        return new(
            false,
            EquivalentExisting: false,
            category,
            categoryCode,
            string.IsNullOrWhiteSpace(reasonCode) ? categoryCode : reasonCode,
            string.IsNullOrWhiteSpace(safeRemediationCode) ? $"{categoryCode}_remediation" : safeRemediationCode,
            category.IsRetryableByDefault(),
            retryAfter,
            request.RepositoryBindingId,
            request.ProviderBindingRef,
            request.CorrelationId,
            SafeTargetFingerprint: null);
    }
}
