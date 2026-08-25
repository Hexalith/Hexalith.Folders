namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderOperationReservationResult(
    ProviderOperationReservationDisposition Disposition,
    string? OperationReference,
    long Generation,
    string? SafeOutcomeFingerprint = null,
    string? ReconciliationReference = null,
    ProviderFailureCategory FailureCategory = ProviderFailureCategory.None,
    string? ReasonCode = null,
    string? RemediationCode = null,
    bool Retryable = false,
    TimeSpan? RetryAfter = null)
{
    public override string ToString() => nameof(ProviderOperationReservationResult);
}
