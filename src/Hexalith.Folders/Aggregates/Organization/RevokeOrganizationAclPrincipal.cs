namespace Hexalith.Folders.Aggregates.Organization;

public sealed record RevokeOrganizationAclPrincipal(
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
    public string CommandType => nameof(RevokeOrganizationAclPrincipal);

    public IReadOnlyList<OrganizationAclOperation> Operations =>
    [
        new OrganizationAclOperation(OrganizationAclOperationIntent.Revoke, PrincipalKind, PrincipalId, Action),
    ];

    public IOrganizationAclCommand WithManagedTenantId(string managedTenantId) => this with { ManagedTenantId = managedTenantId };
}
