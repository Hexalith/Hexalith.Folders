using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspaceCommitRequest(
    string AuthoritativeTenantId,
    string PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string FolderId,
    string RequestSchemaVersion,
    string WorkspaceId,
    string OperationId,
    string AuthorMetadataReference,
    string BranchRefTarget,
    string CommitMessageClassification,
    string ChangedPathMetadataDigest,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId,
    IReadOnlyDictionary<string, string?> ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?> ClientControlledPrincipalValues);
