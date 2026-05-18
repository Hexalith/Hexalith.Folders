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
            validation.IdempotencyFingerprint);

        return FolderResult.Accepted(command, [created]);
    }
}
