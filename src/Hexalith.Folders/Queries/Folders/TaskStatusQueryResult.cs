using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.Folders;

public sealed record TaskStatusQueryResult(
    TaskStatusQueryResultCode Code,
    string? TaskId,
    string CurrentState,
    string? TerminalState,
    string? LastOperationId,
    string? LastFailureCategory,
    WorkspaceStatusRetryEligibility RetryEligibility,
    WorkspaceStatusRetryAfter? RetryAfter,
    FolderLifecycleFreshness Freshness,
    string? CorrelationId,
    TenantAccessAuthorizationResult? AuthorizationDenial);
