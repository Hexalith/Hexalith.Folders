using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.Testing.Factories;

public static class OrganizationAclTestDataFactory
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
    {
        GrantOrganizationAclPrincipal command = new(
            managedTenantId,
            organizationId,
            principalKind,
            principalId,
            action,
            correlationId,
            taskId,
            idempotencyKey,
            payloadTenantId);

        EnsureAccepted(command);
        return command;
    }

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
    {
        RevokeOrganizationAclPrincipal command = new(
            managedTenantId,
            organizationId,
            principalKind,
            principalId,
            action,
            correlationId,
            taskId,
            idempotencyKey,
            payloadTenantId);

        EnsureAccepted(command);
        return command;
    }

    public static OrganizationStreamName OrganizationStreamName(
        string managedTenantId = "tenant-a",
        string organizationId = "organization-a")
        => Aggregates.Organization.OrganizationStreamName.Create(managedTenantId, organizationId);

    private static void EnsureAccepted(IOrganizationAclCommand command)
    {
        OrganizationAclCommandValidationResult result = OrganizationAclCommandValidator.Validate(command);
        if (!result.IsAccepted)
        {
            throw new ArgumentException($"Invalid organization ACL test command: {result.Code}.", nameof(command));
        }
    }
}
