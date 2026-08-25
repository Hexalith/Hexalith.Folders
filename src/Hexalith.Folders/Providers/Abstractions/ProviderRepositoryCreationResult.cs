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
    string? SafeTargetFingerprint,
    string? CanonicalRepositoryId = null,
    string? PriorSafeOutcomeFingerprint = null,
    string? PriorOperationReference = null,
    string? PriorReconciliationReference = null)
{
    public static ProviderRepositoryCreationResult Success(
        ProviderRepositoryCreationRequest request,
        bool equivalentExisting,
        string safeTargetFingerprint,
        string? canonicalRepositoryId = null,
        string? priorSafeOutcomeFingerprint = null,
        string? priorOperationReference = null)
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
            safeTargetFingerprint,
            canonicalRepositoryId,
            priorSafeOutcomeFingerprint,
            priorOperationReference);
    }

    public static ProviderRepositoryCreationResult Failure(
        ProviderRepositoryCreationRequest request,
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null,
        string? safeRemediationCode = null,
        bool? retryable = null,
        string? safeTargetFingerprint = null,
        string? priorSafeOutcomeFingerprint = null,
        string? priorOperationReference = null,
        string? priorReconciliationReference = null)
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
            retryable ?? category.IsRetryableByDefault(),
            retryAfter,
            request.RepositoryBindingId,
            request.ProviderBindingRef,
            request.CorrelationId,
            safeTargetFingerprint,
            CanonicalRepositoryId: null,
            priorSafeOutcomeFingerprint,
            priorOperationReference,
            priorReconciliationReference);
    }
}
