namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Reports metadata-safe evidence for an ordered provider file-change set.
/// </summary>
public sealed record ProviderFileChangeSetResult(
    bool IsSuccess,
    bool EquivalentReplay,
    ProviderFailureCategory FailureCategory,
    string CategoryCode,
    string ReasonCode,
    string SafeRemediationCode,
    bool Retryable,
    TimeSpan? RetryAfter,
    string ProviderBindingRef,
    string ChangeSetReference,
    string CorrelationId,
    string? SafeTargetFingerprint,
    string? SafeStagedChangeSetFingerprint,
    bool ReadOnlyReconciliationSupported,
    string? ReconciliationReference)
{
    /// <summary>Creates a successful staging result.</summary>
    public static ProviderFileChangeSetResult Success(
        ProviderFileChangeSetRequest request,
        string safeTargetFingerprint,
        string safeStagedChangeSetFingerprint,
        bool equivalentReplay = false,
        string? reconciliationReference = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(
            true,
            equivalentReplay,
            ProviderFailureCategory.None,
            ProviderFailureCategory.None.ToCategoryCode(),
            equivalentReplay ? "existing_equivalent" : "success",
            "none",
            Retryable: false,
            RetryAfter: null,
            request.ProviderBindingRef,
            request.ChangeSetReference,
            request.CorrelationId,
            safeTargetFingerprint,
            safeStagedChangeSetFingerprint,
            ReadOnlyReconciliationSupported: false,
            reconciliationReference);
    }

    /// <summary>Creates a safe failed staging result.</summary>
    public static ProviderFileChangeSetResult Failure(
        ProviderFileChangeSetRequest request,
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null,
        string? reconciliationReference = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        string categoryCode = category.ToCategoryCode();
        return new(
            false,
            EquivalentReplay: false,
            category,
            categoryCode,
            string.IsNullOrWhiteSpace(reasonCode) ? categoryCode : reasonCode,
            category == ProviderFailureCategory.UnknownProviderOutcome
                ? "reconciliation_required_metadata_only"
                : $"{categoryCode}_remediation",
            category.IsRetryableByDefault(),
            retryAfter,
            request.ProviderBindingRef,
            request.ChangeSetReference,
            request.CorrelationId,
            SafeTargetFingerprint: null,
            SafeStagedChangeSetFingerprint: null,
            ReadOnlyReconciliationSupported: false,
            reconciliationReference);
    }
}
