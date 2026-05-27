namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspaceCommitExecutionRequest(
    string ManagedTenantId,
    string FolderId,
    string WorkspaceId,
    string OperationId,
    string CorrelationId,
    string TaskId,
    string AuthorMetadataReference,
    string BranchRefTarget,
    string CommitMessageClassification,
    string ChangedPathMetadataDigest);
