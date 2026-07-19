namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderRepositoryCreationTargetResolutionRequest(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    string RepositoryBindingId,
    string RepositoryProfileRef,
    string AuthorizationEvidenceFingerprint,
    string CorrelationId);
