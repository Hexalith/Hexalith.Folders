namespace Hexalith.Folders.Aggregates.Folder;

public sealed record RepositoryBindingFailed(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string RepositoryBindingId,
    string ProviderBindingRef,
    string FailureCategory,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset OccurredAt) : IFolderEvent;
