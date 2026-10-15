using System.Text.Json.Serialization;

namespace Hexalith.Folders.Aggregates.Organization;

public sealed record OrganizationAclPrincipalRevoked(
    string ManagedTenantId,
    string OrganizationId,
    [property: JsonRequired] OrganizationAclPrincipalKind PrincipalKind,
    string PrincipalId,
    string Action,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint) : IOrganizationAclEvent;
