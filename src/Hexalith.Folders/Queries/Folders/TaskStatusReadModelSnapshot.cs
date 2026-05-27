namespace Hexalith.Folders.Queries.Folders;

public sealed record TaskStatusReadModelSnapshot(
    string ManagedTenantId,
    string TaskId,
    string CurrentState,
    string? TerminalState,
    string? LastOperationId,
    string? LastFailureCategory,
    WorkspaceStatusRetryEligibility RetryEligibility,
    WorkspaceStatusRetryAfter? RetryAfter,
    FolderLifecycleFreshness Freshness,
    FolderLifecycleEvidenceScope EvidenceScope);
