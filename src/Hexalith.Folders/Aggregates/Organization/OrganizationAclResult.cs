namespace Hexalith.Folders.Aggregates.Organization;

public sealed record OrganizationAclResult(
    OrganizationAclResultCode Code,
    string? ManagedTenantId,
    string? OrganizationId,
    string? PrincipalId,
    OrganizationAclPrincipalKind? PrincipalKind,
    string? Action,
    string? CorrelationId,
    string? TaskId,
    string? IdempotencyKey,
    IReadOnlyList<IOrganizationAclEvent> Events)
{
    public static OrganizationAclResult Accepted(IOrganizationAclCommand command, IReadOnlyList<IOrganizationAclEvent> events)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        return From(command, OrganizationAclResultCode.Accepted, events);
    }

    public static OrganizationAclResult Rejected(IOrganizationAclCommand command, OrganizationAclResultCode code)
    {
        ArgumentNullException.ThrowIfNull(command);

        return From(command, code, []);
    }

    public static OrganizationAclResult Rejected(
        OrganizationAclResultCode code,
        string? managedTenantId,
        string? organizationId,
        string? correlationId,
        string? taskId,
        string? idempotencyKey)
        => new(code, managedTenantId, organizationId, null, null, null, correlationId, taskId, idempotencyKey, []);

    private static OrganizationAclResult From(
        IOrganizationAclCommand command,
        OrganizationAclResultCode code,
        IReadOnlyList<IOrganizationAclEvent> events)
    {
        OrganizationAclOperation? operation = command.Operations.Count == 1 ? command.Operations[0] : null;
        return new(
            code,
            command.ManagedTenantId,
            command.OrganizationId,
            operation?.PrincipalId,
            operation?.PrincipalKind,
            operation?.Action,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            events);
    }
}
