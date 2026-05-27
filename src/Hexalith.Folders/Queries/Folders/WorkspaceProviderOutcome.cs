namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceProviderOutcome(
    string OperationId,
    string State,
    string SanitizedStatusClass,
    string ProviderCorrelationReference,
    WorkspaceStatusRetryEligibility RetryEligibility,
    WorkspaceStatusRetryAfter? RetryAfter,
    FolderLifecycleFreshness Freshness);
