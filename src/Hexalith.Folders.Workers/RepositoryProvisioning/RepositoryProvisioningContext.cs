using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Workers.RepositoryProvisioning;

public sealed record RepositoryProvisioningContext(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    string ProviderFamily,
    string ProviderKey,
    ProviderTargetEvidence TargetEvidence,
    IReadOnlyList<ProviderCredentialMode> CredentialModeRequirements,
    ProviderAuthorizationEvidenceSnapshot AuthorizationEvidence);
