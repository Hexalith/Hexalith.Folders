namespace Hexalith.Folders.Aggregates.Organization;

public sealed record OrganizationAclBaselineInitialized(
    string ManagedTenantId,
    string OrganizationId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint) : IOrganizationAclEvent;
