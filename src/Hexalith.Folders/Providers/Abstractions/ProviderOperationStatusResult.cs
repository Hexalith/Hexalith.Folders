namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Returns metadata-safe evidence from one read-only provider observation.
/// </summary>
/// <param name="IsSuccess">Whether the status was conclusively observed.</param>
/// <param name="Status">The provider-neutral observed status.</param>
/// <param name="FailureCategory">The canonical failure category.</param>
/// <param name="CategoryCode">The canonical category code.</param>
/// <param name="ReasonCode">The metadata-safe reason code.</param>
/// <param name="SafeRemediationCode">The metadata-safe remediation code.</param>
/// <param name="Retryable">Whether a later authorized status call is permitted.</param>
/// <param name="RetryAfter">Optional bounded retry posture.</param>
/// <param name="CorrelationId">The correlation identifier.</param>
/// <param name="CheckNumber">The authoritative check number that was consumed.</param>
/// <param name="SafeObservedFingerprint">The safe observed ref fingerprint.</param>
/// <param name="ReconciliationReference">The opaque reconciliation identity.</param>
public sealed record ProviderOperationStatusResult(
    bool IsSuccess,
    ProviderOperationStatusKind Status,
    ProviderFailureCategory FailureCategory,
    string CategoryCode,
    string ReasonCode,
    string SafeRemediationCode,
    bool Retryable,
    TimeSpan? RetryAfter,
    string CorrelationId,
    int CheckNumber,
    string? SafeObservedFingerprint,
    string? ReconciliationReference)
{
    /// <inheritdoc />
    public override string ToString() => nameof(ProviderOperationStatusResult);
}
