using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Projections.FolderList;

public sealed record FolderListItem(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string DisplayName,
    string? Description,
    string? PathLabel,
    IReadOnlyList<string> Tags,
    FolderLifecycleState LifecycleState,
    FolderRepositoryBindingState RepositoryBindingState,
    string? RepositoryBindingId,
    string? ProviderBindingRef,
    string? RepositoryProfileRef,
    string? ExternalRepositoryRefFingerprint,
    string? BranchRefPolicyRef,
    string? RepositoryBindingFailureCategory,
    string? RepositoryBindingOutcomeCategory,
    DateTimeOffset? RepositoryBindingUpdatedAt,
    FolderArchiveReasonCode? ArchiveReasonCode,
    string? ArchiveActorPrincipalId,
    string? ArchiveCorrelationId,
    string? ArchiveTaskId,
    string? ArchiveIdempotencyKey,
    DateTimeOffset? ArchivedAt,
    long Sequence);
