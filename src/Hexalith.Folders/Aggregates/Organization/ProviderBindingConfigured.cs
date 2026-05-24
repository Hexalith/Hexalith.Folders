namespace Hexalith.Folders.Aggregates.Organization;

public sealed record ProviderBindingConfigured(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    string ProviderKind,
    string CredentialReferenceId,
    OrganizationProviderBindingPolicy NamingPolicy,
    OrganizationProviderBindingPolicy BranchPolicy,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    string ConfiguredStatus,
    DateTimeOffset OccurredAt) : IOrganizationEvent;
