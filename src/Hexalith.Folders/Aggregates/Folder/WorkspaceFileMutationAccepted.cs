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
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset OccurredAt) : IFolderEvent;
