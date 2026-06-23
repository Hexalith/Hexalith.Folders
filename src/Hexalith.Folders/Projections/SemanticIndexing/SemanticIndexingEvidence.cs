using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Projections.SemanticIndexing;

public sealed record SemanticIndexingEvidence
{
    public SemanticIndexingEvidence(
        string? workflowId = null,
        string? memoryUnitId = null,
        string? resultFingerprint = null,
        string? pathPolicyClass = null,
        long? byteLength = null,
        string? mediaType = null,
        string? transportEvidenceKind = null,
        long? observedByteLength = null)
    {
        if (byteLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength), byteLength, "Content length evidence must not be negative.");
        }

        if (observedByteLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(observedByteLength), observedByteLength, "Observed content length evidence must not be negative.");
        }

        WorkflowId = SemanticIndexingBridgeValidation.RequireOptionalValue(workflowId, nameof(workflowId));
        MemoryUnitId = SemanticIndexingBridgeValidation.RequireOptionalValue(memoryUnitId, nameof(memoryUnitId));
        ResultFingerprint = SemanticIndexingBridgeValidation.RequireOptionalValue(resultFingerprint, nameof(resultFingerprint));
        PathPolicyClass = SemanticIndexingBridgeValidation.RequireOptionalValue(pathPolicyClass, nameof(pathPolicyClass));
        ByteLength = byteLength;
        MediaType = SemanticIndexingBridgeValidation.RequireOptionalValue(mediaType, nameof(mediaType));
        TransportEvidenceKind = SemanticIndexingBridgeValidation.RequireOptionalValue(transportEvidenceKind, nameof(transportEvidenceKind));
        ObservedByteLength = observedByteLength;
    }

    public string? WorkflowId { get; init; }

    public string? MemoryUnitId { get; init; }

    public string? ResultFingerprint { get; init; }

    public string? PathPolicyClass { get; init; }

    public long? ByteLength { get; init; }

    public string? MediaType { get; init; }

    public string? TransportEvidenceKind { get; init; }

    public long? ObservedByteLength { get; init; }

    public static SemanticIndexingEvidence FromMutation(WorkspaceFileMutationAccepted accepted)
    {
        ArgumentNullException.ThrowIfNull(accepted);

        return new SemanticIndexingEvidence(
            pathPolicyClass: accepted.PathPolicyClass,
            byteLength: accepted.ByteLength,
            mediaType: accepted.MediaType,
            transportEvidenceKind: accepted.TransportEvidenceKind,
            observedByteLength: accepted.ObservedByteLength);
    }
}
