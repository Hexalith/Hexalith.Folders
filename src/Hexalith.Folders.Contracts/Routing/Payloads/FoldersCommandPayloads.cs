using System.Text.Json.Serialization;

namespace Hexalith.Folders.Server;

// The SDK compares each installed payload type's full name with the route's
// handler identity. Server links this file as nested FolderDomainProcessor
// types to keep that identity. Contracts compiles it as FoldersCommandPayloads
// (FOLDERS_CONTRACT_PAYLOADS) to export the same wire schemas without Server.
#if FOLDERS_CONTRACT_PAYLOADS
internal static class FoldersCommandPayloads
#else
public sealed partial class FolderDomainProcessor
#endif
{
    internal sealed record ArchiveFolderPayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? ArchiveReasonCode);

    // CreateFolder payload is validated explicitly in ProcessCreateFolderAsync rather than via
    // [JsonRequired]: parentFolderId and metadataClass are optional and GatewayPayloadJsonOptions
    // omits nulls, which would make JsonRequired on those fields fail spuriously.
    internal sealed record CreateFolderPayload(
        string? RequestSchemaVersion,
        string? FolderId,
        string? ParentFolderId,
        CreateFolderMetadataPayload? FolderMetadata);

    internal sealed record CreateFolderMetadataPayload(
        string? DisplayName,
        string? MetadataClass);

    // Folder ACL grant/revoke payload. Operations carry the resolved principal kind/id and the
    // canonical action; the grant-vs-revoke intent comes from the command type, not the payload.
    internal sealed record FolderAccessMutationPayload(
        string? RequestSchemaVersion,
        IReadOnlyList<FolderAccessOperationPayload>? Operations);

    internal sealed record FolderAccessOperationPayload(
        string? PrincipalKind,
        string? PrincipalId,
        string? Action);

    internal sealed record OrganizationAclMutationPayload(
        string? RequestSchemaVersion,
        IReadOnlyList<OrganizationAclOperationPayload>? Operations);

    internal sealed record OrganizationAclOperationPayload(
        string? PrincipalKind,
        string? PrincipalId,
        string? Action);

    internal sealed record ConfigureProviderBindingPayload(
        string? RequestSchemaVersion,
        string? ProviderBindingRef,
        string? ProviderFamilyRef,
        string? CapabilityProfileRef,
        string? NonSecretCredentialReference);

    internal sealed record CreateRepositoryBackedFolderPayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? FolderId,
        [property: JsonRequired] string? RepositoryBindingId,
        [property: JsonRequired] string? ProviderBindingRef,
        [property: JsonRequired] string? RepositoryProfileRef,
        [property: JsonRequired] FolderMetadataPayload? FolderMetadata,
        [property: JsonRequired] BranchRefPolicyPayload? BranchRefPolicy,
        [property: JsonRequired] string? CredentialScopeClass);

    internal sealed record FolderMetadataPayload(
        [property: JsonRequired] string? DisplayName,
        [property: JsonRequired] string? MetadataClass);

    internal sealed record BranchRefPolicyPayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? RepositoryBindingId,
        [property: JsonRequired] string? PolicyRef,
        string? DefaultRef,
        IReadOnlyList<string>? AllowedRefPatterns,
        IReadOnlyList<string>? ProtectedRefPatterns);

    internal sealed record BindRepositoryPayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? ProviderBindingRef,
        [property: JsonRequired] string? ExternalRepositoryRef,
        [property: JsonRequired] BranchRefPolicyPayload? BranchRefPolicy);

    internal sealed record PrepareWorkspacePayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? WorkspaceId,
        [property: JsonRequired] string? RepositoryBindingId,
        [property: JsonRequired] string? BranchRefPolicyRef,
        [property: JsonRequired] string? WorkspacePolicyRef,
        string? TaskId);

    internal sealed record LockWorkspacePayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? WorkspaceId,
        [property: JsonRequired] string? LockIntent,
        [property: JsonRequired] int? RequestedLeaseSeconds,
        string? TaskId);

    internal sealed record ReleaseWorkspaceLockPayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? WorkspaceId,
        [property: JsonRequired] string? LockId,
        [property: JsonRequired] string? LockOwnershipProof,
        [property: JsonRequired] string? ReleaseReasonCode,
        string? TaskId);

    internal sealed record PathMetadataPayload(
        [property: JsonRequired] string? NormalizedPath,
        [property: JsonRequired] string? DisplayName,
        [property: JsonRequired] string? PathPolicyClass,
        [property: JsonRequired] string? UnicodeNormalization);

    internal sealed record FileMutationPayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? WorkspaceId,
        [property: JsonRequired] string? OperationId,
        [property: JsonRequired] string? FileOperationKind,
        [property: JsonRequired] string? TransportOperation,
        [property: JsonRequired] PathMetadataPayload? PathMetadata,
        string? ContentHashReference,
        long? ByteLength,
        string? MediaType,
        string? TransportEvidenceKind,
        long? ObservedByteLength,
        string? TaskId)
    {
        public string? AcceptedSdkKey { get; init; }
        public string? ContentReceiptId { get; init; }
        public long? ExpectedFileVersion { get; init; }
        public long? ExpectedLockFence { get; init; }
        public string? OwnerFileId { get; init; }
        public string? ProjectFileReferenceId { get; init; }
    }

    internal sealed record CommitWorkspacePayload(
        [property: JsonRequired] string? RequestSchemaVersion,
        [property: JsonRequired] string? WorkspaceId,
        [property: JsonRequired] string? OperationId,
        [property: JsonRequired] string? TaskId,
        [property: JsonRequired] string? BranchRefTarget,
        [property: JsonRequired] string? ChangedPathMetadataDigest,
        [property: JsonRequired] string? AuthorMetadataReference,
        [property: JsonRequired] string? CommitMessageClassification,
        [property: JsonRequired] IReadOnlyList<string>? AuditMetadataKeys);
}
