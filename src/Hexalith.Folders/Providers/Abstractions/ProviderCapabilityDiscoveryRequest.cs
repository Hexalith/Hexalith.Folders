namespace Hexalith.Folders.Providers.Abstractions;

public sealed record ProviderCapabilityDiscoveryRequest(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    string ProviderFamily,
    string ProviderKey,
    string ProfileSchemaVersion,
    ProviderTargetEvidence TargetEvidence,
    IReadOnlyList<ProviderCredentialMode> CredentialModeRequirements,
    ProviderAuthorizationEvidenceSnapshot AuthorizationEvidence,
    string CorrelationId);
