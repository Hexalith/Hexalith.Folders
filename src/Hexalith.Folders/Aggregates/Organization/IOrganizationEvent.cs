namespace Hexalith.Folders.Aggregates.Organization;

public interface IOrganizationEvent
{
    string ManagedTenantId { get; }

    string OrganizationId { get; }

    string CorrelationId { get; }

    string TaskId { get; }

    string IdempotencyKey { get; }

    string IdempotencyFingerprint { get; }
}
