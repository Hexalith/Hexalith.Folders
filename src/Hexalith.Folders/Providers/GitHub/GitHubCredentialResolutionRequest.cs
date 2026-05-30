using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubCredentialResolutionRequest(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    string CredentialReferenceId,
    ProviderCredentialMode CredentialMode,
    string AuthorizationEvidenceFingerprint,
    string CorrelationId);
