using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubRepositoryCreationRequest(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    string RepositoryBindingId,
    ProviderRepositoryResolvedTarget Target,
    ProviderCredentialMode CredentialMode,
    string ApiVersion,
    string SafeTargetFingerprint,
    string CorrelationId,
    string IdempotencyKey);
