using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceCleanupStatusQueryResult(
    WorkspaceCleanupStatusQueryResultCode Code,
    string? FolderId,
    string? WorkspaceId,
    string? TaskId,
    string Status,
    string ReasonCode,
    WorkspaceStatusRetryEligibility RetryEligibility,
    FolderLifecycleFreshness Freshness,
    string? CorrelationId,
    DateTimeOffset? ObservedAt,
    DateTimeOffset? LastAttemptedAt,
    LayeredFolderAuthorizationResult? AuthorizationDenial);
