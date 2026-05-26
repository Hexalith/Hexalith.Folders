namespace Hexalith.Folders.Aggregates.Folder;

public static class FolderAggregate
{
    public static FolderResult Handle(FolderState state, CreateFolder command, DateTimeOffset occurredAt)
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
            validation.IdempotencyFingerprint!,
            occurredAt);

        return FolderResult.Accepted(command, [created]);
    }

    public static FolderResult Handle(FolderState state, IFolderAccessCommand command, DateTimeOffset occurredAt)
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

        FolderResultCode activeMutationGuard = FolderActiveMutationGuard.Evaluate(state, FolderActiveMutationCategory.FolderAcl);
        if (activeMutationGuard != FolderResultCode.Accepted)
        {
            return FolderResult.Rejected(command, activeMutationGuard);
        }

        return command switch
        {
            GrantFolderAccess => Grant(state, command, validation, occurredAt),
            RevokeFolderAccess => Revoke(state, command, validation, occurredAt),
            _ => FolderResult.Rejected(command, FolderResultCode.ValidationFailed),
        };
    }

    public static FolderResult Handle(FolderState state, ArchiveFolder command, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        FolderCommandValidationResult validation = FolderCommandValidator.Validate(command);
        if (!validation.IsAccepted)
        {
            return FolderResult.Rejected(command, validation.Code);
        }

        string idempotencyFingerprint = command.DecisionIdempotencyFingerprint ?? validation.IdempotencyFingerprint!;

        // Check folder existence before idempotency. Probing the idempotency map on an
        // uncreated folder could surface IdempotentReplay/IdempotencyConflict and leak
        // whether a prior key was ever applied for a folder the caller cannot observe.
        if (!state.IsCreated)
        {
            return FolderResult.Rejected(command, FolderResultCode.FolderNotFound);
        }

        // Idempotency check BEFORE lifecycle. Same idempotency key already recorded on this
        // stream means the prior archive command was accepted (its FolderArchived event is
        // what put the folder in Archived state), so this is a logical replay, not a new
        // attempt. Different-key archive against an already-archived folder falls through to
        // the AlreadyArchived state check below. This ordering also lets the gate's
        // ResolveAppendConflict reread surface IdempotentReplay/IdempotencyConflict correctly
        // when a racer's archive won the append race.
        if (state.IdempotencyFingerprints.TryGetValue(command.IdempotencyKey, out string? priorFingerprint))
        {
            return string.Equals(priorFingerprint, idempotencyFingerprint, StringComparison.Ordinal)
                ? FolderResult.Rejected(command, FolderResultCode.IdempotentReplay)
                : FolderResult.Rejected(command, FolderResultCode.IdempotencyConflict);
        }

        if (state.LifecycleState == FolderLifecycleState.Archived)
        {
            return FolderResult.Rejected(command, FolderResultCode.AlreadyArchived);
        }

        FolderArchived archived = new(
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            validation.ArchiveReasonCode!.Value,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            idempotencyFingerprint,
            occurredAt);

        return FolderResult.Accepted(command, [archived]);
    }

    public static FolderResult Handle(FolderState state, CreateRepositoryBackedFolder command, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        FolderCommandValidationResult validation = FolderCommandValidator.Validate(command);
        if (!validation.IsAccepted)
        {
            return FolderResult.Rejected(command, validation.Code);
        }

        if (!state.IsCreated)
        {
            return FolderResult.Rejected(command, FolderResultCode.FolderNotFound);
        }

        FolderResultCode activeMutationGuard = FolderActiveMutationGuard.Evaluate(state, FolderActiveMutationCategory.RepositoryBinding);
        if (activeMutationGuard != FolderResultCode.Accepted)
        {
            return FolderResult.Rejected(command, activeMutationGuard);
        }

        if (state.IdempotencyFingerprints.TryGetValue(command.IdempotencyKey, out string? priorFingerprint))
        {
            return string.Equals(priorFingerprint, validation.IdempotencyFingerprint, StringComparison.Ordinal)
                ? FolderResult.Rejected(command, FolderResultCode.IdempotentReplay)
                : FolderResult.Rejected(command, FolderResultCode.IdempotencyConflict);
        }

        if (state.RepositoryBindingState is not null
            && state.RepositoryBindingState != FolderRepositoryBindingState.Unbound)
        {
            return FolderResult.Rejected(command, FolderResultCode.StateTransitionInvalid);
        }

        RepositoryBindingRequested requested = new(
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            command.RepositoryBindingId,
            command.ProviderBindingRef,
            command.RepositoryProfileRef,
            command.BranchRefPolicyRef,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            validation.IdempotencyFingerprint!,
            occurredAt);

        return FolderResult.Accepted(command, [requested]);
    }

    // Convenience overloads for tests that do not care about deterministic timestamps.
    // Production callers must always supply OccurredAt from the gate's TimeProvider so
    // events carry real wall-clock evidence rather than DateTimeOffset.MinValue.
    public static FolderResult Handle(FolderState state, CreateFolder command)
        => Handle(state, command, DateTimeOffset.MinValue);

    public static FolderResult Handle(FolderState state, IFolderAccessCommand command)
        => Handle(state, command, DateTimeOffset.MinValue);

    public static FolderResult Handle(FolderState state, ArchiveFolder command)
        => Handle(state, command, DateTimeOffset.MinValue);

    public static FolderResult Handle(FolderState state, CreateRepositoryBackedFolder command)
        => Handle(state, command, DateTimeOffset.MinValue);

    // Grant emits events per operation, skipping tuples that are already granted. Revoke
    // mirrors this per-op semantics: it emits revoke events for present tuples, skips
    // already-absent tuples, and only signals `MissingEntry` when every requested tuple
    // was already absent. Callers can therefore submit batch grants and batch revokes
    // against partially-stale state and observe the actual delta via the emitted events.
    private static FolderResult Grant(
        FolderState state,
        IFolderAccessCommand command,
        FolderCommandValidationResult validation,
        DateTimeOffset occurredAt)
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
                nextSequence,
                occurredAt));
        }

        return events.Count == 0
            ? FolderResult.Rejected(command, FolderResultCode.AlreadyApplied)
            : FolderResult.Accepted(command, events, DisplayOperation(validation));
    }

    private static FolderResult Revoke(
        FolderState state,
        IFolderAccessCommand command,
        FolderCommandValidationResult validation,
        DateTimeOffset occurredAt)
    {
        List<IFolderEvent> events = [];
        long nextSequence = state.AccessSequence;
        foreach (FolderAccessOperation operation in validation.AccessOperations)
        {
            FolderAccessEntryKey key = KeyFor(command, operation);
            if (!state.HasFolderAccess(key))
            {
                continue;
            }

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
                nextSequence,
                occurredAt));
        }

        return events.Count == 0
            ? FolderResult.Rejected(command, FolderResultCode.MissingEntry)
            : FolderResult.Accepted(command, events, DisplayOperation(validation));
    }

    private static FolderAccessOperation? DisplayOperation(FolderCommandValidationResult validation)
        => validation.AccessOperations.Count == 1 ? validation.AccessOperations[0] : null;

    private static FolderAccessEntryKey KeyFor(IFolderAccessCommand command, FolderAccessOperation operation)
        => new(
            command.ManagedTenantId,
            command.FolderId,
            operation.PrincipalKind,
            operation.PrincipalId,
            operation.Action);
}
