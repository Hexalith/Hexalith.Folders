namespace Hexalith.Folders.Projections.SemanticIndexing;

public sealed record SemanticIndexingEvidence
{
    public SemanticIndexingEvidence(
        string? workflowId = null,
        string? memoryUnitId = null,
        string? resultFingerprint = null)
    {
        WorkflowId = SemanticIndexingBridgeValidation.RequireOptionalValue(workflowId, nameof(workflowId));
        MemoryUnitId = SemanticIndexingBridgeValidation.RequireOptionalValue(memoryUnitId, nameof(memoryUnitId));
        ResultFingerprint = SemanticIndexingBridgeValidation.RequireOptionalValue(resultFingerprint, nameof(resultFingerprint));
    }

    public string? WorkflowId { get; init; }

    public string? MemoryUnitId { get; init; }

    public string? ResultFingerprint { get; init; }
}
