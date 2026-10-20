using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspaceFileMutationRequest(
    string AuthoritativeTenantId,
    string PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string FolderId,
    string RequestSchemaVersion,
    string WorkspaceId,
    string OperationId,
    string FileOperationKind,
    string TransportOperation,
    PathMetadata PathMetadata,
    string? ContentHashReference,
    long? ByteLength,
    string? MediaType,
    string? TransportEvidenceKind,
    long? ObservedByteLength,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId,
    IReadOnlyDictionary<string, string?> ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?> ClientControlledPrincipalValues)
{
    /// <summary>Gets the explicit owner File identity; add operations allocate it at the owner boundary.</summary>
    public string? OwnerFileId { get; init; }

    /// <summary>Gets the explicit Projects-owned reference bound by this owner operation.</summary>
    public string? ProjectFileReferenceId { get; init; }
}
