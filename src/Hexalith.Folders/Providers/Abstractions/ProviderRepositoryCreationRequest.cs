namespace Hexalith.Folders.Providers.Abstractions;

public sealed record ProviderRepositoryCreationRequest(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    string CredentialReferenceId,
    string RepositoryBindingId,
    string ProviderFamily,
    string ProviderKey,
    ProviderTargetEvidence TargetEvidence,
    IReadOnlyList<ProviderCredentialMode> CredentialModeRequirements,
    ProviderAuthorizationEvidenceSnapshot AuthorizationEvidence,
    string CorrelationId,
    string IdempotencyKey);
