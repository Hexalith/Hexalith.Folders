namespace Hexalith.Folders.Aggregates.Folder;

public static class FolderActiveMutationGuard
{
    public static FolderResultCode Evaluate(FolderState state, FolderActiveMutationCategory category)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!Enum.IsDefined(category))
        {
            return FolderResultCode.StateTransitionInvalid;
        }

        if (!state.IsCreated)
        {
            return FolderResultCode.FolderNotFound;
        }

        return state.LifecycleState switch
        {
            FolderLifecycleState.Active => FolderResultCode.Accepted,
            // Use StateTransitionInvalid (category-neutral) so callers of active-only
            // mutations (Grant/Revoke/Workspace/etc.) do not learn whether the folder is
            // specifically archived vs. in any other terminal/transitional state. The
            // ArchiveFolder command path checks LifecycleState directly and surfaces
            // AlreadyArchived through its own response shape.
            FolderLifecycleState.Archived => FolderResultCode.StateTransitionInvalid,
            _ => FolderResultCode.StateTransitionInvalid,
        };
    }
}
