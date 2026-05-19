namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderCreated(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string DisplayName,
    string? Description,
    string? PathLabel,
    IReadOnlyList<string> Tags,
    FolderLifecycleState LifecycleState,
    FolderRepositoryBindingState RepositoryBindingState,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset OccurredAt) : IFolderEvent;
