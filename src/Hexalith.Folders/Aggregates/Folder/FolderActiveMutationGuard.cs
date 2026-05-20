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
            FolderLifecycleState.Archived => FolderResultCode.AlreadyArchived,
            _ => FolderResultCode.StateTransitionInvalid,
        };
    }
}
