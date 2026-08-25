namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Carries caller-authoritative path, type, size, and change-count policy evidence.
/// </summary>
/// <param name="Fingerprint">The safe file-policy evidence fingerprint.</param>
/// <param name="CapturedAt">When the evidence was captured.</param>
/// <param name="FreshnessClass">The evidence freshness class.</param>
/// <param name="MaximumFileBytes">The maximum resolved content size.</param>
/// <param name="MaximumChangeCount">The maximum ordered changes in one set.</param>
/// <param name="AllowsAdd">Whether add operations are allowed.</param>
/// <param name="AllowsChange">Whether change operations are allowed.</param>
/// <param name="AllowsRemove">Whether remove operations are allowed.</param>
public sealed record ProviderFilePolicyEvidence(
    string Fingerprint,
    DateTimeOffset CapturedAt,
    string FreshnessClass,
    int MaximumFileBytes,
    int MaximumChangeCount,
    bool AllowsAdd,
    bool AllowsChange,
    bool AllowsRemove);
