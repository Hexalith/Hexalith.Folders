namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Carries the durable, provider-neutral admission result for a mutating provider intent.
/// </summary>
/// <param name="Disposition">The durable admission decision.</param>
/// <param name="IntentFingerprint">The canonical safe intent fingerprint.</param>
/// <param name="PriorSafeOutcomeFingerprint">The prior safe result fingerprint for an equivalent replay.</param>
/// <param name="PriorReconciliationReference">The prior opaque reconciliation identity, when applicable.</param>
public sealed record ProviderIdempotencyAdmission(
    ProviderIdempotencyDisposition Disposition,
    string IntentFingerprint,
    string? PriorSafeOutcomeFingerprint = null,
    string? PriorReconciliationReference = null);
