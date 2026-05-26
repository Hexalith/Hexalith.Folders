namespace Hexalith.Folders.Aggregates.Folder;

public sealed record ProviderOutcomeUnknown(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string RepositoryBindingId,
    string ProviderBindingRef,
    bool ReconciliationRequired,
    string OutcomeCategory,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset OccurredAt) : IFolderEvent;
