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
    IReadOnlyList<IOrganizationAclEvent> Events,
    OrganizationAclOperation? FailingOperation = null)
{
    public static OrganizationAclResult Accepted(IOrganizationAclCommand command, IReadOnlyList<IOrganizationAclEvent> events)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        return From(command, OrganizationAclResultCode.Accepted, events, failingOperation: null);
    }

    public static OrganizationAclResult Rejected(IOrganizationAclCommand command, OrganizationAclResultCode code)
    {
        ArgumentNullException.ThrowIfNull(command);

        return From(command, code, [], failingOperation: null);
    }

    public static OrganizationAclResult Rejected(
        IOrganizationAclCommand command,
        OrganizationAclResultCode code,
        OrganizationAclOperation? failingOperation)
    {
        ArgumentNullException.ThrowIfNull(command);

        return From(command, code, [], failingOperation);
    }

    public static OrganizationAclResult Rejected(
        OrganizationAclResultCode code,
        string? managedTenantId,
        string? organizationId,
        string? correlationId,
        string? taskId,
        string? idempotencyKey)
        => new(code, managedTenantId, organizationId, null, null, null, correlationId, taskId, idempotencyKey, [], FailingOperation: null);

    private static OrganizationAclResult From(
        IOrganizationAclCommand command,
        OrganizationAclResultCode code,
        IReadOnlyList<IOrganizationAclEvent> events,
        OrganizationAclOperation? failingOperation)
    {
        OrganizationAclOperation? displayOperation = failingOperation
            ?? (command.Operations.Count == 1 ? command.Operations[0] : null);

        OrganizationAclOperation? rejectedOperation = code == OrganizationAclResultCode.Accepted
            ? null
            : failingOperation;

        return new(
            code,
            command.ManagedTenantId,
            command.OrganizationId,
            displayOperation?.PrincipalId,
            displayOperation?.PrincipalKind,
            displayOperation?.Action,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            events,
            rejectedOperation);
    }
}
