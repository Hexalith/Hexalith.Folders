using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoReadinessRequest(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    ProviderCredentialMode CredentialMode,
    string ApiSurfaceVersion,
    string SupportedSnapshotVersion,
    string SafeTargetFingerprint,
    string CorrelationId);
