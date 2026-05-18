namespace Hexalith.Folders.Aggregates.Organization;

public sealed record GrantOrganizationAclPrincipal(
    string ManagedTenantId,
    string OrganizationId,
    OrganizationAclPrincipalKind PrincipalKind,
    string PrincipalId,
    string Action,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId) : IOrganizationAclCommand
{
    public string CommandType => nameof(GrantOrganizationAclPrincipal);

    public IReadOnlyList<OrganizationAclOperation> Operations =>
    [
        new OrganizationAclOperation(OrganizationAclOperationIntent.Grant, PrincipalKind, PrincipalId, Action),
    ];

    public IOrganizationAclCommand WithManagedTenantId(string managedTenantId) => this with { ManagedTenantId = managedTenantId };
}
