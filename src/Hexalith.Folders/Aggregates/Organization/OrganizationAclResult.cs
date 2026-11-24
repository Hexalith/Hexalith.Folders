using System.Text.Json.Serialization;

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
    /// <summary>Gets the zero-based position of the rejected batch operation, when identified.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FailingOperationIndex { get; init; }

    public static OrganizationAclResult Accepted(IOrganizationAclCommand command, IReadOnlyList<IOrganizationAclEvent> events)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        OrganizationAclOperation? operation = SingleOperation(command);
        return new(
            OrganizationAclResultCode.Accepted, command.ManagedTenantId, command.OrganizationId,
            operation?.PrincipalId, operation?.PrincipalKind, operation?.Action,
            command.CorrelationId, command.TaskId, command.IdempotencyKey, events);
    }

    public static OrganizationAclResult Rejected(IOrganizationAclCommand command, OrganizationAclResultCode code)
    {
        return Rejected(command, code, failingOperation: null);
    }

    public static OrganizationAclResult Rejected(
        IOrganizationAclCommand command,
        OrganizationAclResultCode code,
        OrganizationAclOperation? failingOperation)
    {
        ArgumentNullException.ThrowIfNull(command);

        OrganizationAclOperation? operation = SafeOperation(failingOperation ?? SingleOperation(command));
        return new(
            code, SafeEvidence(command.ManagedTenantId), SafeEvidence(command.OrganizationId),
            operation?.PrincipalId, operation?.PrincipalKind, operation?.Action,
            SafeEvidence(command.CorrelationId), SafeEvidence(command.TaskId), SafeEvidence(command.IdempotencyKey),
            [], SafeOperation(failingOperation))
        {
            FailingOperationIndex = FindOperationIndex(command.Operations, failingOperation),
        };
    }

    public static OrganizationAclResult Rejected(
        OrganizationAclResultCode code,
        string? managedTenantId,
        string? organizationId,
        string? correlationId,
        string? taskId,
        string? idempotencyKey)
        => new(code, SafeEvidence(managedTenantId), SafeEvidence(organizationId), null, null, null,
            SafeEvidence(correlationId), SafeEvidence(taskId), SafeEvidence(idempotencyKey), [], FailingOperation: null);

    private static OrganizationAclOperation? SingleOperation(IOrganizationAclCommand command)
        => command.Operations.Count == 1 ? command.Operations[0] : null;

    private static OrganizationAclOperation? SafeOperation(OrganizationAclOperation? operation)
        => operation is not null
            && Enum.IsDefined(operation.PrincipalKind)
            && Enum.IsDefined(operation.Intent)
            && OrganizationAclAction.IsSupported(operation.Action)
            && SafeEvidence(operation.PrincipalId) is not null
                ? operation
                : null;

    private static string? SafeEvidence(string? value)
        => OrganizationAclCommandValidator.IsSafeMetadataIdentifier(value) ? value : null;

    private static int? FindOperationIndex(IReadOnlyList<OrganizationAclOperation> operations, OrganizationAclOperation? failure)
    {
        if (failure is not null)
        {
            for (int index = 0; index < operations.Count; index++)
            {
                if (operations[index] == failure)
                {
                    return index;
                }
            }
        }

        return null;
    }
}
