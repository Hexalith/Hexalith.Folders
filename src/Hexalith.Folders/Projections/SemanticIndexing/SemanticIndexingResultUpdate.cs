namespace Hexalith.Folders.Projections.SemanticIndexing;

public sealed record SemanticIndexingResultUpdate
{
    public SemanticIndexingResultUpdate(
        SemanticIndexingFileVersionIdentity identity,
        SemanticIndexingBridgeStatus status,
        string reasonCode,
        bool retryable,
        string correlationId,
        string taskId,
        string? workflowId,
        string? memoryUnitId,
        string? resultFingerprint,
        DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (status is SemanticIndexingBridgeStatus.Stale or SemanticIndexingBridgeStatus.Tombstoned)
        {
            throw new ArgumentException("Indexing result updates must record an indexing outcome status.", nameof(status));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        Identity = identity;
        Status = status;
        ReasonCode = reasonCode;
        Retryable = retryable;
        CorrelationId = correlationId;
        TaskId = taskId;
        WorkflowId = workflowId;
        MemoryUnitId = memoryUnitId;
        ResultFingerprint = resultFingerprint;
        ObservedAt = observedAt;
    }

    public SemanticIndexingFileVersionIdentity Identity { get; }

    public SemanticIndexingBridgeStatus Status { get; }

    public string ReasonCode { get; }

    public bool Retryable { get; }

    public string CorrelationId { get; }

    public string TaskId { get; }

    public string? WorkflowId { get; }

    public string? MemoryUnitId { get; }

    public string? ResultFingerprint { get; }

    public DateTimeOffset ObservedAt { get; }
}
