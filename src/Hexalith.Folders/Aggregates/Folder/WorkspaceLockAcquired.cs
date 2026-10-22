namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspaceLockAcquired(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string WorkspaceId,
    FolderWorkspaceLifecycleEvent WorkspaceLifecycleEvent,
    string LockId,
    string LockIntent,
    int RequestedLeaseSeconds,
    string HolderTaskId,
    DateTimeOffset AcquiredAt,
    DateTimeOffset EffectiveAt,
    DateTimeOffset ExpiresAt,
    string RetryEligibilityBasis,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset OccurredAt) : IFolderEvent
{
    /// <summary>Gets the monotonic source fence; null preserves historical event bytes.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? LockFence { get; init; }
}
