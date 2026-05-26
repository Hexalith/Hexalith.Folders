using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoRepositoryBindingRequest(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    string RepositoryBindingId,
    string ExternalRepositoryRef,
    string ExternalRepositoryRefFingerprint,
    string BranchRefPolicyRef,
    ProviderCredentialMode CredentialMode,
    string ApiSurfaceVersion,
    string SupportedSnapshotVersion,
    string SafeTargetFingerprint,
    string CorrelationId,
    string IdempotencyKey);
