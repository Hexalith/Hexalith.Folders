namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspaceCommitSucceeded(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string WorkspaceId,
    FolderWorkspaceLifecycleEvent WorkspaceLifecycleEvent,
    string OperationId,
    string CommitReference,
    string ProviderOutcomeCategory,
    string AuthorMetadataReference,
    string BranchRefTarget,
    string CommitMessageClassification,
    string ChangedPathMetadataDigest,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset OccurredAt) : IFolderEvent;
