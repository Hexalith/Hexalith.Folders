namespace Hexalith.Folders.Aggregates.Folder;

public static class FolderAggregate
{
    public static FolderResult Handle(FolderState state, CreateFolder command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        FolderCommandValidationResult validation = FolderCommandValidator.Validate(command);
        if (!validation.IsAccepted)
        {
            return FolderResult.Rejected(command, validation.Code);
        }

        if (state.IdempotencyFingerprints.TryGetValue(command.IdempotencyKey, out string? priorFingerprint))
        {
            return string.Equals(priorFingerprint, validation.IdempotencyFingerprint, StringComparison.Ordinal)
                ? FolderResult.Rejected(command, FolderResultCode.IdempotentReplay)
                : FolderResult.Rejected(command, FolderResultCode.IdempotencyConflict);
        }

        if (state.IsCreated)
        {
            return FolderResult.Rejected(command, FolderResultCode.DuplicateFolder);
        }

        FolderCreated created = new(
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            command.DisplayName.Trim(),
            string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            string.IsNullOrWhiteSpace(command.PathLabel) ? null : command.PathLabel.Trim(),
            validation.CanonicalTags,
            FolderLifecycleState.Active,
            FolderRepositoryBindingState.Unbound,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            validation.IdempotencyFingerprint!);

        return FolderResult.Accepted(command, [created]);
    }

    public static FolderResult Handle(FolderState state, IFolderAccessCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        FolderCommandValidationResult validation = FolderCommandValidator.Validate(command);
        if (!validation.IsAccepted)
        {
            return FolderResult.Rejected(command, validation.Code);
        }

        if (state.IdempotencyFingerprints.TryGetValue(command.IdempotencyKey, out string? priorFingerprint))
        {
            return string.Equals(priorFingerprint, validation.IdempotencyFingerprint, StringComparison.Ordinal)
                ? FolderResult.Rejected(command, FolderResultCode.AlreadyApplied)
                : FolderResult.Rejected(command, FolderResultCode.IdempotencyConflict);
        }

        if (!state.IsCreated)
        {
            return FolderResult.Rejected(command, FolderResultCode.FolderNotFound);
        }

        return command switch
        {
            GrantFolderAccess => Grant(state, command, validation),
            RevokeFolderAccess => Revoke(state, command, validation),
            _ => FolderResult.Rejected(command, FolderResultCode.ValidationFailed),
        };
    }

    private static FolderResult Grant(
        FolderState state,
        IFolderAccessCommand command,
        FolderCommandValidationResult validation)
    {
        List<IFolderEvent> events = [];
        long nextSequence = state.AccessSequence;
        foreach (FolderAccessOperation operation in validation.AccessOperations)
        {
            FolderAccessEntryKey key = KeyFor(command, operation);
            if (state.HasFolderAccess(key))
            {
                continue;
            }

            nextSequence++;
            events.Add(new FolderAccessGranted(
                command.ManagedTenantId,
                command.OrganizationId,
                command.FolderId,
                operation.PrincipalKind,
                operation.PrincipalId,
                operation.Action,
                command.ActorPrincipalId,
                command.CorrelationId,
                command.TaskId,
                command.IdempotencyKey,
                validation.IdempotencyFingerprint!,
                nextSequence));
        }

        return events.Count == 0
            ? FolderResult.Rejected(command, FolderResultCode.AlreadyApplied)
            : FolderResult.Accepted(command, events, validation.AccessOperations.Count == 1 ? validation.AccessOperations[0] : null);
    }

    private static FolderResult Revoke(
        FolderState state,
        IFolderAccessCommand command,
        FolderCommandValidationResult validation)
    {
        foreach (FolderAccessOperation operation in validation.AccessOperations)
        {
            if (!state.HasFolderAccess(KeyFor(command, operation)))
            {
                return FolderResult.Rejected(command, FolderResultCode.MissingEntry);
            }
        }

        List<IFolderEvent> events = [];
        long nextSequence = state.AccessSequence;
        foreach (FolderAccessOperation operation in validation.AccessOperations)
        {
            nextSequence++;
            events.Add(new FolderAccessRevoked(
                command.ManagedTenantId,
                command.OrganizationId,
                command.FolderId,
                operation.PrincipalKind,
                operation.PrincipalId,
                operation.Action,
                command.ActorPrincipalId,
                command.CorrelationId,
                command.TaskId,
                command.IdempotencyKey,
                validation.IdempotencyFingerprint!,
                nextSequence));
        }

        return FolderResult.Accepted(command, events, validation.AccessOperations.Count == 1 ? validation.AccessOperations[0] : null);
    }

    private static FolderAccessEntryKey KeyFor(IFolderAccessCommand command, FolderAccessOperation operation)
        => new(
            command.ManagedTenantId,
            command.FolderId,
            operation.PrincipalKind,
            operation.PrincipalId,
            operation.Action);
}
