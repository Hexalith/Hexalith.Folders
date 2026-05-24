using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubReadinessRequest(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    ProviderCredentialMode CredentialMode,
    string ApiVersion,
    string SafeTargetFingerprint,
    string CorrelationId);

