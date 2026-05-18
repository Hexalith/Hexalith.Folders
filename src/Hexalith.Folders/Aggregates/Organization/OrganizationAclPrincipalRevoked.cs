namespace Hexalith.Folders.Aggregates.Organization;

public sealed record OrganizationAclPrincipalRevoked(
    string ManagedTenantId,
    string OrganizationId,
    OrganizationAclPrincipalKind PrincipalKind,
    string PrincipalId,
    string Action,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint) : IOrganizationAclEvent;
