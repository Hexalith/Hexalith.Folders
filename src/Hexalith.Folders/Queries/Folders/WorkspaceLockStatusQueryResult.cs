using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceLockStatusQueryResult(
    WorkspaceLockStatusQueryResultCode Code,
    string? WorkspaceId,
    string LockState,
    WorkspaceLockLeaseMetadata? Lease,
    WorkspaceLockRetryEligibility RetryEligibility,
    FolderLifecycleFreshness Freshness,
    string? CorrelationId,
    string? TaskId,
    LayeredFolderAuthorizationResult? AuthorizationDenial);
