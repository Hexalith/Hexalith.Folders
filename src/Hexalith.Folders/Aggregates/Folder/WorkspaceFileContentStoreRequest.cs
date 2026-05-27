namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspaceFileContentStoreRequest(
    string ManagedTenantId,
    string FolderId,
    string WorkspaceId,
    string TaskId,
    string OperationId,
    string FileOperationKind,
    string TransportOperation,
    string ContentHashReference,
    long ByteLength,
    string MediaType,
    string TransportEvidenceKind,
    long ObservedByteLength);

