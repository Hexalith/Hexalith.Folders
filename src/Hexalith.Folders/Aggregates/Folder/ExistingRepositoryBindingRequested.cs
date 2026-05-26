namespace Hexalith.Folders.Aggregates.Folder;

public sealed record ExistingRepositoryBindingRequested(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string RepositoryBindingId,
    string ProviderBindingRef,
    string ExternalRepositoryRefFingerprint,
    string BranchRefPolicyRef,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset OccurredAt) : IFolderEvent;
