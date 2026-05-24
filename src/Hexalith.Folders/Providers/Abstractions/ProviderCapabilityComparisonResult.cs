namespace Hexalith.Folders.Providers.Abstractions;

public sealed record ProviderCapabilityComparisonResult(
    bool Equivalent,
    string CurrentFingerprint,
    string CandidateFingerprint,
    IReadOnlyList<string> ChangedDimensions);
