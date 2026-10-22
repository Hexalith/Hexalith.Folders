namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspacePathPolicyEvidenceRequest(
    string ManagedTenantId,
    string FolderId,
    string WorkspaceId,
    string TaskId,
    string OperationId,
    string PathMetadataDigest,
    string PathPolicyClass)
{
    /// <summary>Gets the exact path for current server policy and namespace checks.</summary>
    public PathMetadata? PathMetadata { get; init; }
}
