namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspaceLockReleased(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string WorkspaceId,
    FolderWorkspaceLifecycleEvent WorkspaceLifecycleEvent,
    string LockId,
    string HolderTaskId,
    string ReleaseReasonCode,
    string LeaseStatusBasis,
    DateTimeOffset AcquiredAt,
    DateTimeOffset EffectiveAt,
    DateTimeOffset ExpiresAt,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset OccurredAt) : IFolderEvent;
