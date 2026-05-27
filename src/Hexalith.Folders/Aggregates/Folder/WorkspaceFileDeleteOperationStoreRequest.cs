namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspaceFileDeleteOperationStoreRequest(
    string ManagedTenantId,
    string FolderId,
    string WorkspaceId,
    string TaskId,
    string OperationId,
    string FileOperationKind,
    string TransportOperation,
    string PathMetadataDigest,
    string PathPolicyClass);
