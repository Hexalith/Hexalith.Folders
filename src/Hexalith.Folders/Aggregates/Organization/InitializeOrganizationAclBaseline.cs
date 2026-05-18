namespace Hexalith.Folders.Aggregates.Organization;

public sealed record InitializeOrganizationAclBaseline(
    string ManagedTenantId,
    string OrganizationId,
    IReadOnlyList<OrganizationAclOperation> Operations,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId) : IOrganizationAclCommand
{
    public string CommandType => nameof(InitializeOrganizationAclBaseline);

    public IOrganizationAclCommand WithManagedTenantId(string managedTenantId) => this with { ManagedTenantId = managedTenantId };
}
