namespace Hexalith.Folders.Aggregates.Folder;

/// <summary>Records a folder archive and its durable idempotency evidence.</summary>
/// <param name="ManagedTenantId">The authoritative managed tenant.</param>
/// <param name="OrganizationId">The owning organization.</param>
/// <param name="FolderId">The archived folder.</param>
/// <param name="ArchiveReasonCode">The approved archive reason.</param>
/// <param name="ActorPrincipalId">The principal that archived the folder.</param>
/// <param name="CorrelationId">The correlation identifier for the command.</param>
/// <param name="TaskId">The task associated with the archive.</param>
/// <param name="IdempotencyKey">The archive command's idempotency key.</param>
/// <param name="IdempotencyFingerprint">The non-blank fingerprint recorded for equivalent replay.</param>
/// <param name="OccurredAt">The event timestamp.</param>
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
