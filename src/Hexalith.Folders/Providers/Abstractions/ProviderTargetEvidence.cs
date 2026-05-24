namespace Hexalith.Folders.Providers.Abstractions;

public sealed record ProviderTargetEvidence(
    string Product,
    string ProductVersion,
    string ApiSurfaceVersion,
    string EvidenceVersion,
    bool IsStale,
    DateTimeOffset? ObservedAt,
    IReadOnlyDictionary<string, string> Metadata);
