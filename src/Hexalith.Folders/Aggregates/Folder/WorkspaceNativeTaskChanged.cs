namespace Hexalith.Folders.Aggregates.Folder;

/// <summary>Records a native-only task lease decision and its source-confirmed terminal publication.</summary>
public sealed record WorkspaceNativeTaskChanged(
    string ManagedTenantId, string OrganizationId, string FolderId, string WorkspaceId,
    string Operation, string LockId, long LockFence, int LeaseSeconds,
    DateTimeOffset EffectiveAt, DateTimeOffset ExpiresAt, string? PublicationCommandId,
    string ActorPrincipalId, string CorrelationId, string TaskId,
    string IdempotencyKey, string IdempotencyFingerprint, DateTimeOffset OccurredAt) : IFolderEvent;
