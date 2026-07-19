namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderRepositoryBindingTargetResolutionRequest(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    string RepositoryBindingId,
    string ExternalRepositoryRef,
    string ExternalRepositoryRefFingerprint,
    string BranchRefPolicyRef,
    string AuthorizationEvidenceFingerprint,
    string CorrelationId);
