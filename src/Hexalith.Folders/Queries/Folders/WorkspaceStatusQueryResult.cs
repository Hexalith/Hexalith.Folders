using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceStatusQueryResult(
    WorkspaceStatusQueryResultCode Code,
    string? FolderId,
    string? WorkspaceId,
    string CurrentState,
    WorkspaceAcceptedCommandState? AcceptedCommandState,
    WorkspaceProjectedState? ProjectedState,
    WorkspaceProviderOutcome? ProviderOutcome,
    WorkspaceStatusRetryEligibility RetryEligibility,
    WorkspaceStatusRetryAfter? RetryAfter,
    FolderLifecycleFreshness Freshness,
    WorkspaceProjectionLag ProjectionLag,
    string? LastFailureCategory,
    string? CorrelationId,
    string? TaskId,
    LayeredFolderAuthorizationResult? AuthorizationDenial);
