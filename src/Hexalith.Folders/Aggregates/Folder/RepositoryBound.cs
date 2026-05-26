namespace Hexalith.Folders.Aggregates.Folder;

public sealed record RepositoryBound(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string RepositoryBindingId,
    string ProviderBindingRef,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset OccurredAt) : IFolderEvent;
