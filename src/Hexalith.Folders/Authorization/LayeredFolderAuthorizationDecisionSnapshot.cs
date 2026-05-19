namespace Hexalith.Folders.Authorization;

public sealed record LayeredFolderAuthorizationDecisionSnapshot(
    AuthorizationLayer TerminalLayer,
    string OutcomeCode,
    bool Retryable,
    string FreshnessClass,
    string? FreshnessWatermark,
    string? CorrelationId,
    string? TaskId,
    string ActorSafeIdentifier,
    string OperationPolicyClass,
    string TimingBucket,
    DateTimeOffset DecidedAt);
