namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Returns metadata-safe ordered staging evidence.
/// </summary>
/// <param name="IsSuccess">Whether the changes were staged.</param>
/// <param name="EquivalentReplay">Whether this is a durable equivalent replay.</param>
/// <param name="FailureCategory">The canonical failure category.</param>
/// <param name="CategoryCode">The canonical category code.</param>
/// <param name="ReasonCode">The metadata-safe reason code.</param>
/// <param name="SafeRemediationCode">The metadata-safe remediation code.</param>
/// <param name="Retryable">Whether a later new authorized call may be retried.</param>
/// <param name="RetryAfter">Optional bounded retry posture.</param>
/// <param name="CorrelationId">The correlation identifier.</param>
/// <param name="SafeTargetFingerprint">The safe target fingerprint.</param>
/// <param name="SafeOutcomeFingerprint">The safe staged outcome fingerprint.</param>
/// <param name="OpaqueOperationReference">The opaque staged-operation identity.</param>
/// <param name="ReconciliationReference">The opaque reconciliation identity for an unknown outcome.</param>
public sealed record ProviderFileMutationResult(
    bool IsSuccess,
    bool EquivalentReplay,
    ProviderFailureCategory FailureCategory,
    string CategoryCode,
    string ReasonCode,
    string SafeRemediationCode,
    bool Retryable,
    TimeSpan? RetryAfter,
    string CorrelationId,
    string? SafeTargetFingerprint,
    string? SafeOutcomeFingerprint,
    string? OpaqueOperationReference,
    string? ReconciliationReference)
{
    /// <inheritdoc />
    public override string ToString() => nameof(ProviderFileMutationResult);
}
