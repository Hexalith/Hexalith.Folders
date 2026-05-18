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
            return OrganizationAclResult.Rejected(command, validation.Code, validation.FailingOperation);
        }

        if (state.IdempotencyFingerprints.TryGetValue(command.IdempotencyKey, out string? priorFingerprint))
        {
            return string.Equals(priorFingerprint, validation.IdempotencyFingerprint, StringComparison.Ordinal)
                ? OrganizationAclResult.Rejected(command, OrganizationAclResultCode.AlreadyApplied)
                : OrganizationAclResult.Rejected(command, OrganizationAclResultCode.IdempotencyConflict);
        }

        // Pass 1: detect any operation that must reject the whole batch.
        foreach (OrganizationAclOperation operation in validation.Operations)
        {
            if (operation.Intent != OrganizationAclOperationIntent.Revoke)
            {
                continue;
            }

            OrganizationAclEntryKey key = KeyFor(command, operation);
            if (!state.HasGrant(key))
            {
                return OrganizationAclResult.Rejected(command, OrganizationAclResultCode.MissingEntry, operation);
            }
        }

        // Pass 2: build the event batch from the deltas. Already-granted entries become no-op skips.
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

        bool anyDelta = false;
        foreach (OrganizationAclOperation operation in validation.Operations)
        {
            OrganizationAclEntryKey key = KeyFor(command, operation);

            if (operation.Intent == OrganizationAclOperationIntent.Grant)
            {
                if (state.HasGrant(key))
                {
                    continue;
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
                anyDelta = true;
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
                anyDelta = true;
            }
        }

        if (!anyDelta && state.IsInitialized)
        {
            return OrganizationAclResult.Rejected(command, OrganizationAclResultCode.AlreadyApplied);
        }

        return OrganizationAclResult.Accepted(command, events);
    }

    private static OrganizationAclEntryKey KeyFor(IOrganizationAclCommand command, OrganizationAclOperation operation)
        => new(
            command.ManagedTenantId,
            command.OrganizationId,
            operation.PrincipalKind,
            operation.PrincipalId,
            operation.Action);
}
