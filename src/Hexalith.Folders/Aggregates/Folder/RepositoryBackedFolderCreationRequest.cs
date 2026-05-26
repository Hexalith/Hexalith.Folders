using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed record RepositoryBackedFolderCreationRequest(
    string? AuthoritativeTenantId,
    string PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string FolderId,
    string RequestSchemaVersion,
    string RepositoryBindingId,
    string ProviderBindingRef,
    string RepositoryProfileRef,
    string BranchRefPolicyRef,
    string FolderMetadataDisplayName,
    string CredentialScopeClass,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId,
    IReadOnlyDictionary<string, string?> ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?> ClientControlledPrincipalValues);
