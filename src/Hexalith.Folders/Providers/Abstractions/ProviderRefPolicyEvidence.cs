namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Carries caller-authoritative ref-policy evidence without exposing a ref value.
/// </summary>
/// <param name="Fingerprint">The safe ref-policy evidence fingerprint.</param>
/// <param name="CapturedAt">When the evidence was captured.</param>
/// <param name="FreshnessClass">The evidence freshness class.</param>
/// <param name="AllowsFileMutation">Whether staging is permitted.</param>
/// <param name="AllowsCommit">Whether an explicit commit is permitted.</param>
/// <param name="AllowsNonForceUpdate">Whether one non-force ref update is permitted.</param>
public sealed record ProviderRefPolicyEvidence(
    string Fingerprint,
    DateTimeOffset CapturedAt,
    string FreshnessClass,
    bool AllowsFileMutation,
    bool AllowsCommit,
    bool AllowsNonForceUpdate);
