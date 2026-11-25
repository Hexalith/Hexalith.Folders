namespace Hexalith.Folders.Aggregates.Organization;

public interface IOrganizationAclCommand
{
    string ManagedTenantId { get; }

    string OrganizationId { get; }

    string CorrelationId { get; }

    string TaskId { get; }

    string IdempotencyKey { get; }

    string? PayloadTenantId { get; }

    string CommandType { get; }

    IOrganizationAclCommand WithManagedTenantId(string managedTenantId);

    IReadOnlyList<OrganizationAclOperation> Operations { get; }
}
