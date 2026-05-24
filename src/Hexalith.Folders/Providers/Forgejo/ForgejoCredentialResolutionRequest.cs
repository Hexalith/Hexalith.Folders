using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoCredentialResolutionRequest(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    ProviderCredentialMode CredentialMode,
    string AuthorizationEvidenceFingerprint,
    string CorrelationId);
