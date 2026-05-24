namespace Hexalith.Folders.Aggregates.Organization;

public static class OrganizationAggregate
{
    private const string ConfiguredStatus = "configured";

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

    public static OrganizationProviderBindingResult Handle(OrganizationState state, ConfigureProviderBinding command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        OrganizationProviderBindingCommandValidationResult validation =
            OrganizationProviderBindingCommandValidator.Validate(command);
        if (!validation.IsAccepted)
        {
            return OrganizationProviderBindingResult.Rejected(command, validation.Code);
        }

        if (state.IdempotencyFingerprints.TryGetValue(command.IdempotencyKey, out string? priorFingerprint))
        {
            return string.Equals(priorFingerprint, validation.IdempotencyFingerprint, StringComparison.Ordinal)
                ? OrganizationProviderBindingResult.Rejected(command, OrganizationProviderBindingResultCode.AlreadyApplied)
                : OrganizationProviderBindingResult.Rejected(command, OrganizationProviderBindingResultCode.IdempotencyConflict);
        }

        if (state.ProviderBindings.TryGetValue(command.ProviderBindingRef, out OrganizationProviderBinding? existing))
        {
            string existingFingerprint = OrganizationProviderBindingCommandValidator.Fingerprint(
                command with
                {
                    CredentialReferenceId = existing.CredentialReferenceId,
                    ProviderKind = existing.ProviderKind,
                    NamingPolicy = existing.NamingPolicy,
                    BranchPolicy = existing.BranchPolicy,
                });

            return string.Equals(existingFingerprint, validation.IdempotencyFingerprint, StringComparison.Ordinal)
                ? OrganizationProviderBindingResult.Rejected(command, OrganizationProviderBindingResultCode.AlreadyApplied)
                : OrganizationProviderBindingResult.Rejected(command, OrganizationProviderBindingResultCode.DuplicateConflict);
        }

        IOrganizationEvent[] events =
        [
            new ProviderBindingConfigured(
                command.ManagedTenantId,
                command.OrganizationId,
                command.ProviderBindingRef,
                command.ProviderKind,
                command.CredentialReferenceId,
                command.NamingPolicy,
                command.BranchPolicy,
                command.CorrelationId,
                command.TaskId,
                command.IdempotencyKey,
                validation.IdempotencyFingerprint,
                ConfiguredStatus,
                command.OccurredAt),
        ];

        return OrganizationProviderBindingResult.Accepted(command, events);
    }

    private static OrganizationAclEntryKey KeyFor(IOrganizationAclCommand command, OrganizationAclOperation operation)
        => new(
            command.ManagedTenantId,
            command.OrganizationId,
            operation.PrincipalKind,
            operation.PrincipalId,
            operation.Action);
}
