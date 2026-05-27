namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceLockStatusReadModelSnapshot(
    string ManagedTenantId,
    string FolderId,
    string WorkspaceId,
    string WorkspaceState,
    string LockState,
    string? LockId,
    string? HolderTaskId,
    DateTimeOffset? AcquiredAt,
    DateTimeOffset? EffectiveAt,
    DateTimeOffset? ExpiresAt,
    string? RetryEligibilityBasis,
    string? CorrelationId,
    string? TaskId,
    FolderLifecycleFreshness Freshness,
    FolderLifecycleEvidenceScope EvidenceScope);
