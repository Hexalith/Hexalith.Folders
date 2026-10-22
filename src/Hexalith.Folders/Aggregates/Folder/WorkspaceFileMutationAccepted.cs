namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspaceFileMutationAccepted(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string WorkspaceId,
    FolderWorkspaceLifecycleEvent WorkspaceLifecycleEvent,
    string OperationId,
    string FileOperationKind,
    string TransportOperation,
    string PathPolicyClass,
    string PathMetadataDigest,
    string? ContentHashReference,
    long? ByteLength,
    string? MediaType,
    string? TransportEvidenceKind,
    long? ObservedByteLength,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset OccurredAt) : IFolderEvent
{
    /// <summary>Gets the verified, body-free publication binding; absent on historical metadata-only events.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public WorkspaceContentBinding? ContentBinding { get; init; }

    /// <summary>Gets the explicit owner File identity; add operations allocate it at the owner boundary.</summary>
    public string? OwnerFileId { get; init; }

    /// <summary>Gets the explicit Projects-owned reference bound by this owner operation.</summary>
    public string? ProjectFileReferenceId { get; init; }

    /// <summary>Gets the native exact workspace/path address digest used only to invalidate or verify declared lineage, independent of display metadata.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? MutationTargetDigest { get; init; }
}
