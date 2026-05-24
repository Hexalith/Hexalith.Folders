namespace Hexalith.Folders.Aggregates.Organization;

public sealed record ConfigureProviderBinding(
    string ManagedTenantId,
    string OrganizationId,
    OrganizationAclPrincipalKind ActorPrincipalKind,
    string ActorPrincipalId,
    string ProviderBindingRef,
    string ProviderKind,
    string CredentialReferenceId,
    OrganizationProviderBindingPolicy NamingPolicy,
    OrganizationProviderBindingPolicy BranchPolicy,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    DateTimeOffset OccurredAt,
    string? PayloadTenantId)
{
    public string CommandType => nameof(ConfigureProviderBinding);

    public ConfigureProviderBinding WithManagedTenantId(string managedTenantId) => this with { ManagedTenantId = managedTenantId };
}
