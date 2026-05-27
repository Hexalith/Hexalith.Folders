namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderWorkspaceTransitionResult(
    bool IsAccepted,
    FolderWorkspaceLifecycleState? CurrentState,
    FolderWorkspaceLifecycleEvent AttemptedEvent,
    FolderWorkspaceLifecycleState? NextState,
    FolderResultCode Code,
    FolderOperatorDisposition? OperatorDisposition);
