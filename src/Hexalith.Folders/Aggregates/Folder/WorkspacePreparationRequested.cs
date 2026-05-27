namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspacePreparationRequested(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string WorkspaceId,
    string RepositoryBindingId,
    string BranchRefPolicyRef,
    string WorkspacePolicyRef,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset OccurredAt) : IFolderEvent;
