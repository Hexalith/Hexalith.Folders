namespace Hexalith.Folders.Providers.Abstractions;

public sealed record ProviderAuthorizationEvidenceSnapshot(
    string Fingerprint,
    DateTimeOffset CapturedAt,
    string FreshnessClass);
