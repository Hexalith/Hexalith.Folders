using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoRepositoryCreationRequest(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    string RepositoryBindingId,
    ProviderCredentialMode CredentialMode,
    string ApiSurfaceVersion,
    string SupportedSnapshotVersion,
    string SafeTargetFingerprint,
    string CorrelationId,
    string IdempotencyKey);
