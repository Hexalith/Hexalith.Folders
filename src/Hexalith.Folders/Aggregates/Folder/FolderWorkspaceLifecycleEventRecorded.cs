namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderWorkspaceLifecycleEventRecorded(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string WorkspaceId,
    FolderWorkspaceLifecycleEvent WorkspaceLifecycleEvent,
    FolderWorkspaceDirtyResolution? DirtyResolution,
    string OperationId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset OccurredAt) : IFolderEvent;
