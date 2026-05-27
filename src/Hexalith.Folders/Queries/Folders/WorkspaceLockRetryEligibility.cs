namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceLockRetryEligibility(
    bool Retryable,
    int? RetryAfterSeconds,
    string ReasonCode,
    string? CorrelationId,
    string? TaskId,
    string CurrentState,
    FolderLifecycleFreshness Freshness);
