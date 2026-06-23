using Hexalith.Folders.Projections.SemanticIndexing;

namespace Hexalith.Folders.Workers.SemanticIndexing;

public interface ISemanticIndexingPolicyEvaluator
{
    ValueTask<SemanticIndexingPolicyEvaluationResult> EvaluateAsync(
        SemanticIndexingBridgeEntry entry,
        CancellationToken cancellationToken);
}

public sealed record SemanticIndexingPolicyEvaluationResult
{
    private SemanticIndexingPolicyEvaluationResult(
        SemanticIndexingPolicyEvaluationStatus status,
        string reasonCode,
        bool retryable,
        string sensitivityClassification,
        string pathPolicyOutcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(sensitivityClassification);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathPolicyOutcome);

        Status = status;
        ReasonCode = reasonCode;
        Retryable = retryable;
        SensitivityClassification = sensitivityClassification;
        PathPolicyOutcome = pathPolicyOutcome;
    }

    public SemanticIndexingPolicyEvaluationStatus Status { get; }

    public string ReasonCode { get; }

    public bool Retryable { get; }

    public string SensitivityClassification { get; }

    public string PathPolicyOutcome { get; }

    public bool IsAllowed => Status == SemanticIndexingPolicyEvaluationStatus.Allowed;

    public static SemanticIndexingPolicyEvaluationResult Allowed(
        string sensitivityClassification,
        string pathPolicyOutcome)
        => new(
            SemanticIndexingPolicyEvaluationStatus.Allowed,
            "policy_allowed",
            retryable: false,
            sensitivityClassification,
            pathPolicyOutcome);

    public static SemanticIndexingPolicyEvaluationResult Skipped(string reasonCode, bool retryable)
        => new(
            SemanticIndexingPolicyEvaluationStatus.Skipped,
            reasonCode,
            retryable,
            "unknown",
            "denied");

    public static SemanticIndexingPolicyEvaluationResult Failed(string reasonCode, bool retryable)
        => new(
            SemanticIndexingPolicyEvaluationStatus.Failed,
            reasonCode,
            retryable,
            "unknown",
            "unavailable");
}

public enum SemanticIndexingPolicyEvaluationStatus
{
    Allowed,
    Skipped,
    Failed,
}
