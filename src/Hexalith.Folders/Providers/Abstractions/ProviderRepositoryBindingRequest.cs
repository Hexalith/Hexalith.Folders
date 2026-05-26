namespace Hexalith.Folders.Providers.Abstractions;

public sealed record ProviderRepositoryBindingRequest(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    string RepositoryBindingId,
    string ExternalRepositoryRef,
    string ExternalRepositoryRefFingerprint,
    string BranchRefPolicyRef,
    string ProviderFamily,
    string ProviderKey,
    ProviderTargetEvidence TargetEvidence,
    IReadOnlyList<ProviderCredentialMode> CredentialModeRequirements,
    ProviderAuthorizationEvidenceSnapshot AuthorizationEvidence,
    string CorrelationId,
    string IdempotencyKey);
