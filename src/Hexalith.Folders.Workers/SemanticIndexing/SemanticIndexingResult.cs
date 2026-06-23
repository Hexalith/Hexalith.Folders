using System.Globalization;

namespace Hexalith.Folders.Workers.SemanticIndexing;

public sealed record SemanticIndexingResult
{
    public SemanticIndexingResult(
        SemanticIndexingStatus status,
        string reasonCode,
        bool retryable,
        string? memoryUnitId = null,
        string? workflowInstanceId = null)
    {
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(reasonCode);

        Status = status;
        ReasonCode = reasonCode;
        Retryable = retryable;
        MemoryUnitId = memoryUnitId;
        WorkflowInstanceId = workflowInstanceId;
    }

    public SemanticIndexingStatus Status { get; init; }

    public string StatusCode => Status.ToString().ToLower(CultureInfo.InvariantCulture);

    public string ReasonCode { get; init; }

    public bool Retryable { get; init; }

    public string? MemoryUnitId { get; init; }

    public string? WorkflowInstanceId { get; init; }
}
