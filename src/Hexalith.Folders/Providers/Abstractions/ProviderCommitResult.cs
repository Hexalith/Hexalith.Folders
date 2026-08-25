namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Returns metadata-safe explicit commit evidence only after the intended ref update is confirmed.
/// </summary>
/// <param name="IsSuccess">Whether the commit and ref update were confirmed.</param>
/// <param name="EquivalentReplay">Whether this is a durable equivalent replay.</param>
/// <param name="FailureCategory">The canonical failure category.</param>
/// <param name="CategoryCode">The canonical category code.</param>
/// <param name="ReasonCode">The metadata-safe reason code.</param>
/// <param name="SafeRemediationCode">The metadata-safe remediation code.</param>
/// <param name="Retryable">Whether a later new authorized call may be retried.</param>
/// <param name="RetryAfter">Optional bounded retry posture.</param>
/// <param name="CorrelationId">The correlation identifier.</param>
/// <param name="SafeTargetFingerprint">The safe target fingerprint.</param>
/// <param name="SafeCommitFingerprint">The safe confirmed commit fingerprint.</param>
/// <param name="OpaqueOperationReference">The opaque commit-operation identity.</param>
/// <param name="ReconciliationReference">The opaque reconciliation identity for an unknown outcome.</param>
public sealed record ProviderCommitResult(
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
    string? SafeCommitFingerprint,
    string? OpaqueOperationReference,
    string? ReconciliationReference)
{
    /// <inheritdoc />
    public override string ToString() => nameof(ProviderCommitResult);
}
