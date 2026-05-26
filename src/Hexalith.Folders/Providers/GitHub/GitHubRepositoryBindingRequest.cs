using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubRepositoryBindingRequest(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    string RepositoryBindingId,
    string ExternalRepositoryRef,
    string ExternalRepositoryRefFingerprint,
    string BranchRefPolicyRef,
    ProviderCredentialMode CredentialMode,
    string ApiVersion,
    string SafeTargetFingerprint,
    string CorrelationId,
    string IdempotencyKey);
