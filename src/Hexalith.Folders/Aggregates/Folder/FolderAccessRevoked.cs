namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderAccessRevoked(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    FolderAccessPrincipalKind PrincipalKind,
    string PrincipalId,
    string Action,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    long AccessSequence) : IFolderEvent;
