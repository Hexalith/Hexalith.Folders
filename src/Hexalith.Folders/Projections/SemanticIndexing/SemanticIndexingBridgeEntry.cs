namespace Hexalith.Folders.Projections.SemanticIndexing;

public sealed record SemanticIndexingBridgeEntry
{
    public SemanticIndexingBridgeEntry(
        SemanticIndexingFileVersionIdentity identity,
        SemanticIndexingBridgeStatus status,
        string reasonCode,
        bool retryable,
        string correlationId,
        string taskId,
        DateTimeOffset statusObservedAt,
        SemanticIndexingEvidence? evidence = null,
        SemanticIndexingProjectionFreshness? freshness = null,
        SemanticIndexingCommitEvidence? commitEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        Identity = identity;
        Status = status;
        StatusCode = status.ToStatusCode();
        ReasonCode = reasonCode;
        Retryable = retryable;
        CorrelationId = correlationId;
        TaskId = taskId;
        StatusObservedAt = statusObservedAt;
        Evidence = evidence ?? new SemanticIndexingEvidence();
        Freshness = freshness ?? new SemanticIndexingProjectionFreshness();
        CommitEvidence = commitEvidence;
    }

    public SemanticIndexingFileVersionIdentity Identity { get; init; }

    public SemanticIndexingBridgeStatus Status { get; init; }

    public string StatusCode { get; init; }

    public string ReasonCode { get; init; }

    public bool Retryable { get; init; }

    public string CorrelationId { get; init; }

    public string TaskId { get; init; }

    public DateTimeOffset StatusObservedAt { get; init; }

    public SemanticIndexingEvidence Evidence { get; init; }

    public SemanticIndexingProjectionFreshness Freshness { get; init; }

    public SemanticIndexingCommitEvidence? CommitEvidence { get; init; }
}
