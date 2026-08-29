namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Carries the durable, provider-neutral admission result for a mutating provider intent.
/// </summary>
/// <param name="Disposition">The durable admission decision.</param>
/// <param name="IntentFingerprint">The canonical safe intent fingerprint.</param>
/// <param name="PriorSafeOutcomeFingerprint">The prior safe result fingerprint for an equivalent replay.</param>
/// <param name="PriorReconciliationReference">The prior opaque reconciliation identity, when applicable.</param>
/// <param name="PriorOperationReference">The exact prior opaque operation identity.</param>
/// <param name="PriorOutcomeDisposition">The exact prior terminal disposition.</param>
/// <param name="PriorFailureCategory">The exact prior known-terminal category.</param>
/// <param name="PriorReasonCode">The exact prior allow-listed reason.</param>
/// <param name="PriorRemediationCode">The exact prior allow-listed remediation.</param>
/// <param name="PriorRetryable">Whether the exact prior known-terminal result was retryable.</param>
/// <param name="PriorRetryAfter">The exact prior bounded retry posture.</param>
/// <param name="PriorCanonicalRepositoryId">The exact prior canonical repository identity for a repository success.</param>
public sealed record ProviderIdempotencyAdmission(
    ProviderIdempotencyDisposition Disposition,
    string IntentFingerprint,
    string? PriorSafeOutcomeFingerprint = null,
    string? PriorReconciliationReference = null,
    string? PriorOperationReference = null,
    ProviderPriorOutcomeDisposition? PriorOutcomeDisposition = null,
    ProviderFailureCategory PriorFailureCategory = ProviderFailureCategory.None,
    string? PriorReasonCode = null,
    string? PriorRemediationCode = null,
    bool PriorRetryable = false,
    TimeSpan? PriorRetryAfter = null,
    string? PriorCanonicalRepositoryId = null)
{
    /// <summary>
    /// Preserves the original eleven-parameter constructor for already-compiled consumers.
    /// </summary>
    public ProviderIdempotencyAdmission(
        ProviderIdempotencyDisposition disposition,
        string intentFingerprint,
        string? priorSafeOutcomeFingerprint,
        string? priorReconciliationReference,
        string? priorOperationReference,
        ProviderPriorOutcomeDisposition? priorOutcomeDisposition,
        ProviderFailureCategory priorFailureCategory,
        string? priorReasonCode,
        string? priorRemediationCode,
        bool priorRetryable,
        TimeSpan? priorRetryAfter)
        : this(
            disposition,
            intentFingerprint,
            priorSafeOutcomeFingerprint,
            priorReconciliationReference,
            priorOperationReference,
            priorOutcomeDisposition,
            priorFailureCategory,
            priorReasonCode,
            priorRemediationCode,
            priorRetryable,
            priorRetryAfter,
            PriorCanonicalRepositoryId: null)
    {
    }
}
