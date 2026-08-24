namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Reports metadata-safe evidence for an explicit provider commit.
/// </summary>
public sealed record ProviderCommitResult(
    bool IsSuccess,
    bool EquivalentReplay,
    ProviderFailureCategory FailureCategory,
    string CategoryCode,
    string ReasonCode,
    string SafeRemediationCode,
    bool Retryable,
    TimeSpan? RetryAfter,
    string ProviderBindingRef,
    string StagedChangeSetReference,
    string CorrelationId,
    string? SafeTargetFingerprint,
    string? SafeCommitFingerprint,
    string? SafeExpectedCommitFingerprint,
    string? ReconciliationReference)
{
    /// <summary>Creates a successful explicit-commit result.</summary>
    public static ProviderCommitResult Success(
        ProviderCommitRequest request,
        string safeTargetFingerprint,
        string safeCommitFingerprint,
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
            request.StagedChangeSetReference,
            request.CorrelationId,
            safeTargetFingerprint,
            safeCommitFingerprint,
            safeCommitFingerprint,
            reconciliationReference);
    }

    /// <summary>Creates a safe failed explicit-commit result.</summary>
    public static ProviderCommitResult Failure(
        ProviderCommitRequest request,
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null,
        string? reconciliationReference = null,
        string? safeExpectedCommitFingerprint = null)
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
            request.StagedChangeSetReference,
            request.CorrelationId,
            SafeTargetFingerprint: null,
            SafeCommitFingerprint: null,
            safeExpectedCommitFingerprint,
            reconciliationReference);
    }
}
