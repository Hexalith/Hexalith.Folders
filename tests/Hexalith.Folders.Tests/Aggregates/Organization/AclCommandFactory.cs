using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

internal static class AclCommandFactory
{
    public static GrantOrganizationAclPrincipal Grant(
        string managedTenantId = "tenant-a",
        string organizationId = "organization-a",
        OrganizationAclPrincipalKind principalKind = OrganizationAclPrincipalKind.User,
        string principalId = "principal-a",
        string action = "read_metadata",
        string correlationId = "correlation-a",
        string taskId = "task-a",
        string idempotencyKey = "idempotency-a",
        string? payloadTenantId = null)
        => new(
            managedTenantId,
            organizationId,
            principalKind,
            principalId,
            action,
            correlationId,
            taskId,
            idempotencyKey,
            payloadTenantId);

    public static RevokeOrganizationAclPrincipal Revoke(
        string managedTenantId = "tenant-a",
        string organizationId = "organization-a",
        OrganizationAclPrincipalKind principalKind = OrganizationAclPrincipalKind.User,
        string principalId = "principal-a",
        string action = "read_metadata",
        string correlationId = "correlation-a",
        string taskId = "task-a",
        string idempotencyKey = "idempotency-a",
        string? payloadTenantId = null)
        => new(
            managedTenantId,
            organizationId,
            principalKind,
            principalId,
            action,
            correlationId,
            taskId,
            idempotencyKey,
            payloadTenantId);

    public static InitializeOrganizationAclBaseline Initialize(params OrganizationAclOperation[] operations)
        => Initialize("idempotency-a", operations);

    public static InitializeOrganizationAclBaseline Initialize(string idempotencyKey, params OrganizationAclOperation[] operations)
        => new(
            "tenant-a",
            "organization-a",
            operations,
            "correlation-a",
            "task-a",
            idempotencyKey,
            null);

    public static OrganizationAclOperation Operation(
        OrganizationAclOperationIntent intent,
        OrganizationAclPrincipalKind principalKind = OrganizationAclPrincipalKind.User,
        string principalId = "principal-a",
        string action = "read_metadata")
        => new(intent, principalKind, principalId, action);
}
