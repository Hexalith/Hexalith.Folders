namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceStatusReadModelSnapshot(
    string ManagedTenantId,
    string FolderId,
    string WorkspaceId,
    string CurrentState,
    WorkspaceAcceptedCommandState? AcceptedCommandState,
    WorkspaceProjectedState ProjectedState,
    WorkspaceProviderOutcome ProviderOutcome,
    WorkspaceStatusRetryEligibility RetryEligibility,
    WorkspaceStatusRetryAfter? RetryAfter,
    FolderLifecycleFreshness Freshness,
    WorkspaceProjectionLag ProjectionLag,
    string? LastFailureCategory,
    FolderLifecycleEvidenceScope EvidenceScope);
