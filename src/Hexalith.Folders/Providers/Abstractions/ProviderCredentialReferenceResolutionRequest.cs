namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderCredentialReferenceResolutionRequest(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    string CredentialReferenceId,
    string ProviderFamily,
    string ProviderKey,
    ProviderCredentialMode CredentialMode,
    string AuthorizationEvidenceFingerprint,
    string CorrelationId);
