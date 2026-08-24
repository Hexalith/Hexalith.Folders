namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Reports a metadata-safe read-only provider reconciliation outcome.
/// </summary>
public sealed record ProviderMutationStatusResult(
    ProviderMutationStatusDisposition Disposition,
    ProviderFailureCategory FailureCategory,
    string CategoryCode,
    string ReasonCode,
    string SafeRemediationCode,
    bool Retryable,
    TimeSpan? RetryAfter,
    string ProviderBindingRef,
    string OperationReference,
    string CorrelationId,
    string ReconciliationReference,
    string? SafeCommitFingerprint)
{
    /// <summary>Creates an available status result.</summary>
    public static ProviderMutationStatusResult Available(
        ProviderMutationStatusRequest request,
        ProviderMutationStatusDisposition disposition,
        string? safeCommitFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (disposition is not ProviderMutationStatusDisposition.Confirmed
            and not ProviderMutationStatusDisposition.NotApplied
            and not ProviderMutationStatusDisposition.Conflicting)
        {
            return Unavailable(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                "provider_status_disposition_invalid");
        }

        ProviderFailureCategory category = disposition == ProviderMutationStatusDisposition.Conflicting
            ? ProviderFailureCategory.ReconciliationRequired
            : ProviderFailureCategory.None;
        return new(
            disposition,
            category,
            category.ToCategoryCode(),
            disposition switch
            {
                ProviderMutationStatusDisposition.Confirmed => "provider_outcome_confirmed",
                ProviderMutationStatusDisposition.NotApplied => "provider_outcome_not_applied",
                _ => "provider_outcome_conflicting",
            },
            disposition == ProviderMutationStatusDisposition.Conflicting
                ? "reconciliation_required_metadata_only"
                : "none",
            Retryable: false,
            RetryAfter: null,
            request.ProviderBindingRef,
            request.OperationReference,
            request.CorrelationId,
            request.ReconciliationReference,
            safeCommitFingerprint);
    }

    /// <summary>Creates an unavailable or rejected status result.</summary>
    public static ProviderMutationStatusResult Unavailable(
        ProviderMutationStatusRequest request,
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        string categoryCode = category.ToCategoryCode();
        return new(
            ProviderMutationStatusDisposition.Unavailable,
            category,
            categoryCode,
            string.IsNullOrWhiteSpace(reasonCode) ? categoryCode : reasonCode,
            category == ProviderFailureCategory.ReconciliationRequired
                ? "reconciliation_required_metadata_only"
                : $"{categoryCode}_remediation",
            category.IsRetryableByDefault(),
            retryAfter,
            request.ProviderBindingRef,
            request.OperationReference,
            request.CorrelationId,
            request.ReconciliationReference,
            SafeCommitFingerprint: null);
    }
}
