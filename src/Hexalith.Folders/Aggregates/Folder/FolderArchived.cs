namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderArchived(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    FolderArchiveReasonCode ArchiveReasonCode,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset OccurredAt) : IFolderEvent;
