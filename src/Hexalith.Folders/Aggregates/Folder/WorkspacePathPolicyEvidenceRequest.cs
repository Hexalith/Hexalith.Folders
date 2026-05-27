namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspacePathPolicyEvidenceRequest(
    string ManagedTenantId,
    string FolderId,
    string WorkspaceId,
    string TaskId,
    string OperationId,
    string PathMetadataDigest,
    string PathPolicyClass);
