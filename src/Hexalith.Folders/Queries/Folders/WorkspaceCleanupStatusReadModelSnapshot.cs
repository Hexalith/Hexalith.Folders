namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceCleanupStatusReadModelSnapshot(
    string ManagedTenantId,
    string FolderId,
    string WorkspaceId,
    string? TaskId,
    string Status,
    string ReasonCode,
    WorkspaceStatusRetryEligibility RetryEligibility,
    FolderLifecycleFreshness Freshness,
    string? CorrelationId,
    DateTimeOffset? ObservedAt,
    DateTimeOffset? LastAttemptedAt,
    FolderLifecycleEvidenceScope EvidenceScope);
