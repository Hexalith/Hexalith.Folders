namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed record ProviderSupportEvidenceItem(
    string CapabilityProfileRef,
    string Capability,
    string SupportState);
