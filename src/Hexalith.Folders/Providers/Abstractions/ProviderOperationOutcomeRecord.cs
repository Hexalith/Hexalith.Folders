namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderOperationOutcomeRecord(
    string OperationReference,
    long Generation,
    ProviderOperationOutcomeKind Kind,
    string? PrivateObjectId,
    string? SafeOutcomeFingerprint,
    ProviderFailureCategory FailureCategory,
    string ReasonCode,
    string? RemediationCode = null,
    bool Retryable = false,
    TimeSpan? RetryAfter = null,
    string? ReconciliationReference = null)
{
    public override string ToString() => nameof(ProviderOperationOutcomeRecord);
}
