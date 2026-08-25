namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Carries caller-authoritative canonical lock evidence without exposing a lock value.
/// </summary>
/// <param name="Fingerprint">The safe lock evidence fingerprint.</param>
/// <param name="CapturedAt">When the evidence was captured.</param>
/// <param name="FreshnessClass">The evidence freshness class.</param>
/// <param name="IsOwnedByDelegatedTask">Whether the delegated task owns the canonical lock.</param>
/// <param name="IsRevoked">Whether the lock instance is revoked.</param>
public sealed record ProviderOperationLockEvidence(
    string Fingerprint,
    DateTimeOffset CapturedAt,
    string FreshnessClass,
    bool IsOwnedByDelegatedTask,
    bool IsRevoked);
