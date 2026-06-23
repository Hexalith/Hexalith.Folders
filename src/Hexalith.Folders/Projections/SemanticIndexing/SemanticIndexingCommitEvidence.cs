namespace Hexalith.Folders.Projections.SemanticIndexing;

public sealed record SemanticIndexingCommitEvidence
{
    public SemanticIndexingCommitEvidence(
        bool? succeeded,
        string operationId,
        string providerOutcomeCategory,
        string? failureCategory,
        string correlationId,
        string taskId,
        DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerOutcomeCategory);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        Succeeded = succeeded;
        OperationId = operationId;
        ProviderOutcomeCategory = providerOutcomeCategory;
        FailureCategory = failureCategory;
        CorrelationId = correlationId;
        TaskId = taskId;
        OccurredAt = occurredAt;
    }

    public bool? Succeeded { get; init; }

    public string OperationId { get; init; }

    public string ProviderOutcomeCategory { get; init; }

    public string? FailureCategory { get; init; }

    public string CorrelationId { get; init; }

    public string TaskId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}
