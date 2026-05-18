namespace Hexalith.Folders.Aggregates.Organization;

public static class OrganizationAggregate
{
    public static OrganizationAclResult Handle(OrganizationState state, IOrganizationAclCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        OrganizationAclCommandValidationResult validation = OrganizationAclCommandValidator.Validate(command);
        if (!validation.IsAccepted)
        {
            return OrganizationAclResult.Rejected(command, validation.Code);
        }

        if (state.IdempotencyFingerprints.TryGetValue(command.IdempotencyKey, out string? priorFingerprint))
        {
            return string.Equals(priorFingerprint, validation.IdempotencyFingerprint, StringComparison.Ordinal)
                ? OrganizationAclResult.Rejected(command, OrganizationAclResultCode.AlreadyApplied)
                : OrganizationAclResult.Rejected(command, OrganizationAclResultCode.IdempotencyConflict);
        }

        List<IOrganizationAclEvent> events = [];
        if (!state.IsInitialized)
        {
            events.Add(new OrganizationAclBaselineInitialized(
                command.ManagedTenantId,
                command.OrganizationId,
                command.CorrelationId,
                command.TaskId,
                command.IdempotencyKey,
                validation.IdempotencyFingerprint));
        }

        foreach (OrganizationAclOperation operation in validation.Operations)
        {
            OrganizationAclEntryKey key = new(
                command.ManagedTenantId,
                command.OrganizationId,
                operation.PrincipalKind,
                operation.PrincipalId,
                operation.Action);

            if (operation.Intent == OrganizationAclOperationIntent.Grant)
            {
                if (state.HasGrant(key))
                {
                    return events.Count == 0
                        ? OrganizationAclResult.Rejected(command, OrganizationAclResultCode.AlreadyApplied)
                        : OrganizationAclResult.Accepted(command, events);
                }

                events.Add(new OrganizationAclPrincipalGranted(
                    command.ManagedTenantId,
                    command.OrganizationId,
                    operation.PrincipalKind,
                    operation.PrincipalId,
                    operation.Action,
                    command.CorrelationId,
                    command.TaskId,
                    command.IdempotencyKey,
                    validation.IdempotencyFingerprint));
            }
            else if (!state.HasGrant(key))
            {
                return OrganizationAclResult.Rejected(command, OrganizationAclResultCode.MissingEntry);
            }
            else
            {
                events.Add(new OrganizationAclPrincipalRevoked(
                    command.ManagedTenantId,
                    command.OrganizationId,
                    operation.PrincipalKind,
                    operation.PrincipalId,
                    operation.Action,
                    command.CorrelationId,
                    command.TaskId,
                    command.IdempotencyKey,
                    validation.IdempotencyFingerprint));
            }
        }

        return events.Count == 0
            ? OrganizationAclResult.Rejected(command, OrganizationAclResultCode.AlreadyApplied)
            : OrganizationAclResult.Accepted(command, events);
    }

}
