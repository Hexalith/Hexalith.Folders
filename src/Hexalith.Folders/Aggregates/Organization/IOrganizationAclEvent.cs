namespace Hexalith.Folders.Aggregates.Organization;

public interface IOrganizationAclEvent
{
    string ManagedTenantId { get; }

    string OrganizationId { get; }

    string CorrelationId { get; }

    string TaskId { get; }

    string IdempotencyKey { get; }

    string IdempotencyFingerprint { get; }
}
