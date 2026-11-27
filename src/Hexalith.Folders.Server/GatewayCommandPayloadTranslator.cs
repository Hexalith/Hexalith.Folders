using System.Collections.Frozen;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Contracts.Commands.Models;

using AggPathMetadata = Hexalith.Folders.Aggregates.Folder.PathMetadata;

namespace Hexalith.Folders.Server;

/// <summary>
/// Translates and strictly validates Gateway-dispatched decorated McpCli v2 command payloads (C01–C13)
/// into the domain payload records consumed by <see cref="FolderDomainProcessor"/>.
/// </summary>
internal static partial class GatewayCommandPayloadTranslator
{
    private const int InlineContentByteLimit = 262_144;
    private const int StreamContentMinimumBytes = 262_145;
    private const int MaximumFileBytes = 1_048_576;
    private const long MaximumBatchBytes = 10_485_760;
    private const int MaximumInlineBase64Length = 349_528;
    private const int MaximumAuditMetadataKeys = 24;
    private const int MaximumFileBatchItems = 100;
    private const string DomainAcceptedSchemaVersion = "v1";
    private const string FixedCredentialScopeClass = "provider_binding";

    private static readonly FrozenSet<string> ValidArchiveReasonCodes =
        FrozenSet.ToFrozenSet(["caller_requested", "policy_retention", "operator_review"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> ValidMetadataTiers =
        FrozenSet.ToFrozenSet(["public_metadata", "tenant_sensitive", "credential_sensitive", "secret"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> ValidReleaseReasonCodes =
        FrozenSet.ToFrozenSet(
            ["caller_completed", "caller_abandoned", "operator_requested", "authorization_revoked", "task_cancelled", "lock_revoked"],
            StringComparer.Ordinal);

    private static readonly FrozenSet<string> ValidPathPolicyClasses =
        FrozenSet.ToFrozenSet(["content_allowed", "metadata_only", "excluded", "restricted"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> ValidAuditMetadataKeys = BuildAuditMetadataKeySet();

    private static readonly FrozenSet<string> ArchiveFolderProperties =
        FrozenSet.ToFrozenSet(["archiveReasonCode", "folderId", "idempotencyKey"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> BindRepositoryProperties =
        FrozenSet.ToFrozenSet(["providerBindingRef", "externalRepositoryRef", "branchRefPolicy", "folderId", "idempotencyKey"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> CommitWorkspaceProperties =
        FrozenSet.ToFrozenSet(
            [
                "operationId", "taskId", "branchRefTarget", "changedPathMetadataDigest",
                "authorMetadataReference", "commitMessageClassification", "auditMetadataKeys",
                "folderId", "workspaceId", "idempotencyKey",
            ],
            StringComparer.Ordinal);

    private static readonly FrozenSet<string> ConfigureBranchRefPolicyProperties =
        FrozenSet.ToFrozenSet(
            ["repositoryBindingId", "policyRef", "defaultRef", "allowedRefPatterns", "folderId", "idempotencyKey", "protectedRefPatterns"],
            StringComparer.Ordinal);

    private static readonly FrozenSet<string> BranchRefPolicyNestedProperties =
        FrozenSet.ToFrozenSet(
            ["repositoryBindingId", "policyRef", "defaultRef", "allowedRefPatterns", "protectedRefPatterns"],
            StringComparer.Ordinal);

    private static readonly FrozenSet<string> ConfigureProviderBindingProperties =
        FrozenSet.ToFrozenSet(
            ["providerFamilyRef", "capabilityProfileRef", "nonSecretCredentialReference", "providerBindingRef", "idempotencyKey"],
            StringComparer.Ordinal);

    private static readonly FrozenSet<string> CreateFolderProperties =
        FrozenSet.ToFrozenSet(["folderMetadata", "idempotencyKey", "folderId", "parentFolderId"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> CreateRepositoryBackedFolderProperties =
        FrozenSet.ToFrozenSet(
            ["folderId", "providerBindingRef", "repositoryProfileRef", "folderMetadata", "branchRefPolicy", "idempotencyKey"],
            StringComparer.Ordinal);

    private static readonly FrozenSet<string> FolderMetadataProperties =
        FrozenSet.ToFrozenSet(["displayName", "metadataClass"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> FolderAccessProperties =
        FrozenSet.ToFrozenSet(["subjectRef", "permissionLevel", "folderId", "aclEntryId", "idempotencyKey"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> LockWorkspaceProperties =
        FrozenSet.ToFrozenSet(["lockIntent", "requestedLeaseSeconds", "folderId", "workspaceId", "idempotencyKey", "taskId"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> PrepareWorkspaceProperties =
        FrozenSet.ToFrozenSet(
            ["repositoryBindingId", "branchRefPolicyRef", "workspacePolicyRef", "folderId", "workspaceId", "idempotencyKey", "taskId"],
            StringComparer.Ordinal);

    private static readonly FrozenSet<string> ReleaseWorkspaceLockProperties =
        FrozenSet.ToFrozenSet(
            ["lockId", "lockOwnershipProof", "releaseReasonCode", "folderId", "workspaceId", "idempotencyKey", "taskId"],
            StringComparer.Ordinal);

    private static readonly FrozenSet<string> MutateFilesRootProperties =
        FrozenSet.ToFrozenSet(["changes", "folderId", "workspaceId", "idempotencyKey", "taskId"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> FileMutationItemProperties =
        FrozenSet.ToFrozenSet(
            [
                "expectedFileVersion", "expectedLockFence", "fileOperationKind", "transportOperation",
                "operationId", "pathMetadata", "ownerFileId", "contentHashReference",
                "byteLength", "inlineContent", "streamDescriptor",
            ],
            StringComparer.Ordinal);

    private static readonly FrozenSet<string> PathMetadataProperties =
        FrozenSet.ToFrozenSet(["normalizedPath", "displayName", "pathPolicyClass", "unicodeNormalization"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> PutFileInlineProperties =
        FrozenSet.ToFrozenSet(["mediaType", "contentBytes", "contentMediaType"], StringComparer.Ordinal);

    private static readonly FrozenSet<string> PutFileStreamProperties =
        FrozenSet.ToFrozenSet(
            ["mediaType", "declaredLength", "observedLength", "stagingReference", "observedContentHashReference", "uploadMode"],
            StringComparer.Ordinal);

    /// <summary>
    /// Determines whether the raw payload JSON omits <c>requestSchemaVersion</c> at its root object
    /// and therefore must be validated and translated as a decorated v2 command payload.
    /// </summary>
    /// <param name="rawPayload">The UTF-8 command payload bytes.</param>
    /// <returns><see langword="true"/> when the payload is a valid JSON object without <c>requestSchemaVersion</c>.</returns>
    internal static bool IsDecoratedV2Payload(ReadOnlyMemory<byte> rawPayload)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(rawPayload);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && !document.RootElement.TryGetProperty("requestSchemaVersion", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether a C08/C13 folder-access payload is a decorated v2 payload rather than a legacy v1
    /// <c>operations[]</c> payload (which may omit <c>requestSchemaVersion</c>).
    /// </summary>
    /// <param name="rawPayload">The UTF-8 command payload bytes.</param>
    /// <returns><see langword="true"/> when the payload is a decorated v2 folder-access payload.</returns>
    internal static bool IsDecoratedV2FolderAccessPayload(ReadOnlyMemory<byte> rawPayload)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(rawPayload);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && !document.RootElement.TryGetProperty("requestSchemaVersion", out _)
                && !document.RootElement.TryGetProperty("operations", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns whether <paramref name="auditMetadataKeys"/> satisfies the closed <see cref="AuditMetadataKey"/>
    /// vocabulary, count (1..24), and uniqueness constraints.
    /// </summary>
    /// <param name="auditMetadataKeys">The audit metadata keys to validate.</param>
    /// <param name="allowEmpty">Whether an empty array is permitted (legacy v1 compatibility only).</param>
    /// <returns><see langword="true"/> when valid.</returns>
    internal static bool AreValidAuditMetadataKeys(IReadOnlyList<string>? auditMetadataKeys, bool allowEmpty)
    {
        if (auditMetadataKeys is null)
        {
            return false;
        }

        if (auditMetadataKeys.Count == 0)
        {
            return allowEmpty;
        }

        if (auditMetadataKeys.Count > MaximumAuditMetadataKeys)
        {
            return false;
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string key in auditMetadataKeys)
        {
            if (string.IsNullOrWhiteSpace(key)
                || !ValidAuditMetadataKeys.Contains(key)
                || !seen.Add(key))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Translates a decorated v2 C01 <c>ArchiveFolder</c> payload.</summary>
    internal static bool TryTranslateArchiveFolder(
        CommandEnvelope envelope,
        out FolderDomainProcessor.ArchiveFolderPayload? payload,
        out FolderResultCode failureCode)
    {
        payload = null;
        if (!TryParseRoot(envelope.Payload, ArchiveFolderProperties, out JsonDocument? document, out failureCode))
        {
            return false;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!TryReadRequiredString(root, "archiveReasonCode", out string? reason, out failureCode)
                || !TryReadRequiredMatchingFolderId(root, envelope.AggregateId, out failureCode)
                || !TryReadRequiredIdempotencyKey(root, out failureCode))
            {
                return false;
            }

            if (!ValidArchiveReasonCodes.Contains(reason!))
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            payload = new FolderDomainProcessor.ArchiveFolderPayload(DomainAcceptedSchemaVersion, reason);
            failureCode = FolderResultCode.Accepted;
            return true;
        }
    }

    /// <summary>Translates a decorated v2 C02 <c>BindRepository</c> payload.</summary>
    internal static bool TryTranslateBindRepository(
        CommandEnvelope envelope,
        out FolderDomainProcessor.BindRepositoryPayload? payload,
        out FolderResultCode failureCode)
    {
        payload = null;
        if (!TryParseRoot(envelope.Payload, BindRepositoryProperties, out JsonDocument? document, out failureCode))
        {
            return false;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!TryReadRequiredIdentifier(root, "providerBindingRef", out string? providerBindingRef, out failureCode)
                || !TryReadRequiredIdentifier(root, "externalRepositoryRef", out string? externalRepositoryRef, out failureCode)
                || !TryReadRequiredMatchingFolderId(root, envelope.AggregateId, out failureCode)
                || !TryReadRequiredIdempotencyKey(root, out failureCode)
                || !TryReadRequiredObject(root, "branchRefPolicy", out JsonElement branchPolicyElement, out failureCode)
                || !TryTranslateBranchRefPolicyObject(branchPolicyElement, BranchRefPolicyNestedProperties, out FolderDomainProcessor.BranchRefPolicyPayload? branchPolicy, out failureCode))
            {
                return false;
            }

            payload = new FolderDomainProcessor.BindRepositoryPayload(
                DomainAcceptedSchemaVersion,
                providerBindingRef,
                externalRepositoryRef,
                branchPolicy);
            failureCode = FolderResultCode.Accepted;
            return true;
        }
    }

    /// <summary>Translates a decorated v2 C03 <c>CommitWorkspace</c> payload.</summary>
    internal static bool TryTranslateCommitWorkspace(
        CommandEnvelope envelope,
        string expectedTaskId,
        out FolderDomainProcessor.CommitWorkspacePayload? payload,
        out FolderResultCode failureCode)
    {
        payload = null;
        if (!TryParseRoot(envelope.Payload, CommitWorkspaceProperties, out JsonDocument? document, out failureCode))
        {
            return false;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!TryReadRequiredIdentifier(root, "operationId", out string? operationId, out failureCode)
                || !TryReadRequiredMatchingTaskId(root, expectedTaskId, out string? taskId, out failureCode)
                || !TryReadRequiredString(root, "branchRefTarget", out string? branchRefTarget, out failureCode)
                || !TryReadRequiredString(root, "changedPathMetadataDigest", out string? changedPathMetadataDigest, out failureCode)
                || !TryReadRequiredString(root, "authorMetadataReference", out string? authorMetadataReference, out failureCode)
                || !TryReadRequiredString(root, "commitMessageClassification", out string? commitMessageClassification, out failureCode)
                || !TryReadRequiredMatchingFolderId(root, envelope.AggregateId, out failureCode)
                || !TryReadRequiredIdentifier(root, "workspaceId", out string? workspaceId, out failureCode)
                || !TryReadRequiredIdempotencyKey(root, out failureCode)
                || !TryReadRequiredStringArray(root, "auditMetadataKeys", out string[]? auditMetadataKeys, out failureCode))
            {
                return false;
            }

            if (!AreValidAuditMetadataKeys(auditMetadataKeys, allowEmpty: false))
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            payload = new FolderDomainProcessor.CommitWorkspacePayload(
                DomainAcceptedSchemaVersion,
                workspaceId,
                operationId,
                taskId,
                branchRefTarget,
                changedPathMetadataDigest,
                authorMetadataReference,
                commitMessageClassification,
                auditMetadataKeys);
            failureCode = FolderResultCode.Accepted;
            return true;
        }
    }

    /// <summary>Translates a decorated v2 C04 <c>ConfigureBranchRefPolicy</c> payload.</summary>
    internal static bool TryTranslateConfigureBranchRefPolicy(
        CommandEnvelope envelope,
        out FolderDomainProcessor.BranchRefPolicyPayload? payload,
        out FolderResultCode failureCode)
    {
        payload = null;
        if (!TryParseRoot(envelope.Payload, ConfigureBranchRefPolicyProperties, out JsonDocument? document, out failureCode))
        {
            return false;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!TryReadRequiredMatchingFolderId(root, envelope.AggregateId, out failureCode)
                || !TryReadRequiredIdempotencyKey(root, out failureCode)
                || !TryTranslateBranchRefPolicyObject(root, ConfigureBranchRefPolicyProperties, out payload, out failureCode))
            {
                return false;
            }

            failureCode = FolderResultCode.Accepted;
            return true;
        }
    }

    /// <summary>Translates a decorated v2 C05 <c>ConfigureProviderBinding</c> payload.</summary>
    internal static bool TryTranslateConfigureProviderBinding(
        CommandEnvelope envelope,
        out FolderDomainProcessor.ConfigureProviderBindingPayload? payload,
        out FolderResultCode failureCode)
    {
        payload = null;
        if (!TryParseRoot(envelope.Payload, ConfigureProviderBindingProperties, out JsonDocument? document, out failureCode))
        {
            return false;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!TryReadRequiredIdentifier(root, "providerFamilyRef", out string? providerFamilyRef, out failureCode)
                || !TryReadRequiredIdentifier(root, "capabilityProfileRef", out string? capabilityProfileRef, out failureCode)
                || !TryReadRequiredIdentifier(root, "nonSecretCredentialReference", out string? nonSecretCredentialReference, out failureCode)
                || !TryReadRequiredIdentifier(root, "providerBindingRef", out string? providerBindingRef, out failureCode)
                || !TryReadRequiredIdempotencyKey(root, out failureCode))
            {
                return false;
            }

            if (string.Equals(envelope.AggregateId, envelope.TenantId, StringComparison.Ordinal)
                || !string.Equals(ProviderBindingActorId.Create(providerBindingRef!), envelope.AggregateId, StringComparison.Ordinal))
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            payload = new FolderDomainProcessor.ConfigureProviderBindingPayload(
                DomainAcceptedSchemaVersion,
                providerBindingRef,
                providerFamilyRef,
                capabilityProfileRef,
                nonSecretCredentialReference);
            failureCode = FolderResultCode.Accepted;
            return true;
        }
    }

    /// <summary>Translates a decorated v2 C06 <c>CreateFolder</c> payload.</summary>
    internal static bool TryTranslateCreateFolder(
        CommandEnvelope envelope,
        out FolderDomainProcessor.CreateFolderPayload? payload,
        out FolderResultCode failureCode)
    {
        payload = null;
        if (!TryParseRoot(envelope.Payload, CreateFolderProperties, out JsonDocument? document, out failureCode))
        {
            return false;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!TryReadRequiredMatchingFolderId(root, envelope.AggregateId, out failureCode)
                || !TryReadRequiredIdempotencyKey(root, out failureCode)
                || !TryReadOptionalIdentifier(root, "parentFolderId", out string? parentFolderId, out failureCode)
                || !TryReadRequiredObject(root, "folderMetadata", out JsonElement metadataElement, out failureCode)
                || !TryReadFolderMetadata(metadataElement, out string? displayName, out string? metadataClass, out failureCode))
            {
                return false;
            }

            payload = new FolderDomainProcessor.CreateFolderPayload(
                DomainAcceptedSchemaVersion,
                envelope.AggregateId,
                parentFolderId,
                new FolderDomainProcessor.CreateFolderMetadataPayload(displayName, metadataClass));
            failureCode = FolderResultCode.Accepted;
            return true;
        }
    }

    /// <summary>Translates a decorated v2 C07 <c>CreateRepositoryBackedFolder</c> payload.</summary>
    internal static bool TryTranslateCreateRepositoryBackedFolder(
        CommandEnvelope envelope,
        out FolderDomainProcessor.CreateRepositoryBackedFolderPayload? payload,
        out FolderResultCode failureCode)
    {
        payload = null;
        if (!TryParseRoot(envelope.Payload, CreateRepositoryBackedFolderProperties, out JsonDocument? document, out failureCode))
        {
            return false;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!TryReadRequiredMatchingFolderId(root, envelope.AggregateId, out failureCode)
                || !TryReadRequiredIdentifier(root, "providerBindingRef", out string? providerBindingRef, out failureCode)
                || !TryReadRequiredIdentifier(root, "repositoryProfileRef", out string? repositoryProfileRef, out failureCode)
                || !TryReadRequiredIdempotencyKey(root, out failureCode)
                || !TryReadRequiredObject(root, "folderMetadata", out JsonElement metadataElement, out failureCode)
                || !TryReadFolderMetadata(metadataElement, out string? displayName, out string? metadataClass, out failureCode)
                || !TryReadRequiredObject(root, "branchRefPolicy", out JsonElement branchPolicyElement, out failureCode)
                || !TryTranslateBranchRefPolicyObject(branchPolicyElement, BranchRefPolicyNestedProperties, out FolderDomainProcessor.BranchRefPolicyPayload? branchPolicy, out failureCode))
            {
                return false;
            }

            payload = new FolderDomainProcessor.CreateRepositoryBackedFolderPayload(
                DomainAcceptedSchemaVersion,
                envelope.AggregateId,
                branchPolicy!.RepositoryBindingId,
                providerBindingRef,
                repositoryProfileRef,
                new FolderDomainProcessor.FolderMetadataPayload(displayName, metadataClass),
                branchPolicy,
                FixedCredentialScopeClass);
            failureCode = FolderResultCode.Accepted;
            return true;
        }
    }

    /// <summary>
    /// Translates a decorated v2 C08 (<c>GrantFolderAccess</c>) or C13 (<c>RevokeFolderAccess</c>) payload.
    /// Rejects any caller-supplied <c>effect</c> property because grant/revoke intent is derived solely
    /// from the command wire type.
    /// </summary>
    internal static bool TryTranslateFolderAccess(
        CommandEnvelope envelope,
        out FolderDomainProcessor.FolderAccessMutationPayload? payload,
        out FolderResultCode failureCode)
    {
        payload = null;
        if (!TryParseRoot(envelope.Payload, FolderAccessProperties, out JsonDocument? document, out failureCode))
        {
            return false;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!TryReadRequiredString(root, "subjectRef", out string? subjectRef, out failureCode)
                || !TryReadRequiredString(root, "permissionLevel", out string? permissionLevel, out failureCode)
                || !TryReadRequiredMatchingFolderId(root, envelope.AggregateId, out failureCode)
                || !TryReadRequiredString(root, "aclEntryId", out string? aclEntryId, out failureCode)
                || !TryReadRequiredIdempotencyKey(root, out failureCode))
            {
                return false;
            }

            string? action = FolderAclContract.PermissionLevelToAction(permissionLevel);
            if (action is null
                || !TryParseDecoratedSubjectRef(subjectRef!, out string principalKindToken, out string principalId))
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            string expectedAclEntryId = FolderAclContract.DeriveAclEntryId(principalKindToken, principalId, permissionLevel!);
            if (!string.Equals(aclEntryId, expectedAclEntryId, StringComparison.Ordinal))
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            payload = new FolderDomainProcessor.FolderAccessMutationPayload(
                DomainAcceptedSchemaVersion,
                [new FolderDomainProcessor.FolderAccessOperationPayload(principalKindToken, principalId, action)]);
            failureCode = FolderResultCode.Accepted;
            return true;
        }
    }

    /// <summary>Translates a decorated v2 C09 <c>LockWorkspace</c> payload.</summary>
    internal static bool TryTranslateLockWorkspace(
        CommandEnvelope envelope,
        string expectedTaskId,
        out FolderDomainProcessor.LockWorkspacePayload? payload,
        out FolderResultCode failureCode)
    {
        payload = null;
        if (!TryParseRoot(envelope.Payload, LockWorkspaceProperties, out JsonDocument? document, out failureCode))
        {
            return false;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!TryReadRequiredString(root, "lockIntent", out string? lockIntent, out failureCode)
                || !TryReadRequiredInt32(root, "requestedLeaseSeconds", out int requestedLeaseSeconds, out failureCode)
                || !TryReadRequiredMatchingFolderId(root, envelope.AggregateId, out failureCode)
                || !TryReadRequiredIdentifier(root, "workspaceId", out string? workspaceId, out failureCode)
                || !TryReadRequiredIdempotencyKey(root, out failureCode)
                || !TryReadRequiredMatchingTaskId(root, expectedTaskId, out string? taskId, out failureCode))
            {
                return false;
            }

            if (!string.Equals(lockIntent, "exclusive_write", StringComparison.Ordinal)
                || requestedLeaseSeconds is < 1 or > 86_400)
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            payload = new FolderDomainProcessor.LockWorkspacePayload(
                DomainAcceptedSchemaVersion,
                workspaceId,
                lockIntent,
                requestedLeaseSeconds,
                taskId);
            failureCode = FolderResultCode.Accepted;
            return true;
        }
    }

    /// <summary>Translates a decorated v2 C11 <c>PrepareWorkspace</c> payload.</summary>
    internal static bool TryTranslatePrepareWorkspace(
        CommandEnvelope envelope,
        string expectedTaskId,
        out FolderDomainProcessor.PrepareWorkspacePayload? payload,
        out FolderResultCode failureCode)
    {
        payload = null;
        if (!TryParseRoot(envelope.Payload, PrepareWorkspaceProperties, out JsonDocument? document, out failureCode))
        {
            return false;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!TryReadRequiredIdentifier(root, "repositoryBindingId", out string? repositoryBindingId, out failureCode)
                || !TryReadRequiredIdentifier(root, "branchRefPolicyRef", out string? branchRefPolicyRef, out failureCode)
                || !TryReadRequiredIdentifier(root, "workspacePolicyRef", out string? workspacePolicyRef, out failureCode)
                || !TryReadRequiredMatchingFolderId(root, envelope.AggregateId, out failureCode)
                || !TryReadRequiredIdentifier(root, "workspaceId", out string? workspaceId, out failureCode)
                || !TryReadRequiredIdempotencyKey(root, out failureCode)
                || !TryReadRequiredMatchingTaskId(root, expectedTaskId, out string? taskId, out failureCode))
            {
                return false;
            }

            payload = new FolderDomainProcessor.PrepareWorkspacePayload(
                DomainAcceptedSchemaVersion,
                workspaceId,
                repositoryBindingId,
                branchRefPolicyRef,
                workspacePolicyRef,
                taskId);
            failureCode = FolderResultCode.Accepted;
            return true;
        }
    }

    /// <summary>Translates a decorated v2 C12 <c>ReleaseWorkspaceLock</c> payload.</summary>
    internal static bool TryTranslateReleaseWorkspaceLock(
        CommandEnvelope envelope,
        string expectedTaskId,
        out FolderDomainProcessor.ReleaseWorkspaceLockPayload? payload,
        out FolderResultCode failureCode)
    {
        payload = null;
        if (!TryParseRoot(envelope.Payload, ReleaseWorkspaceLockProperties, out JsonDocument? document, out failureCode))
        {
            return false;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!TryReadRequiredIdentifier(root, "lockId", out string? lockId, out failureCode)
                || !TryReadRequiredIdentifier(root, "lockOwnershipProof", out string? lockOwnershipProof, out failureCode)
                || !TryReadRequiredString(root, "releaseReasonCode", out string? releaseReasonCode, out failureCode)
                || !TryReadRequiredMatchingFolderId(root, envelope.AggregateId, out failureCode)
                || !TryReadRequiredIdentifier(root, "workspaceId", out string? workspaceId, out failureCode)
                || !TryReadRequiredIdempotencyKey(root, out failureCode)
                || !TryReadRequiredMatchingTaskId(root, expectedTaskId, out string? taskId, out failureCode))
            {
                return false;
            }

            if (!ValidReleaseReasonCodes.Contains(releaseReasonCode!))
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            payload = new FolderDomainProcessor.ReleaseWorkspaceLockPayload(
                DomainAcceptedSchemaVersion,
                workspaceId,
                lockId,
                lockOwnershipProof,
                releaseReasonCode,
                taskId);
            failureCode = FolderResultCode.Accepted;
            return true;
        }
    }

    /// <summary>
    /// Validates and translates a decorated v2 C10 <c>MutateFiles</c> <c>changes[]</c> batch into
    /// ordered file mutation items ready for content-store receipt materialization.
    /// </summary>
    internal static bool TryTranslateMutateFilesBatch(
        CommandEnvelope envelope,
        string expectedTaskId,
        out string? workspaceId,
        out string? acceptedIdempotencyKey,
        out IReadOnlyList<TranslatedFileMutationItem>? items,
        out FolderResultCode failureCode)
    {
        workspaceId = null;
        acceptedIdempotencyKey = null;
        items = null;

        if (!TryParseRoot(envelope.Payload, MutateFilesRootProperties, out JsonDocument? document, out failureCode))
        {
            return false;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!TryReadRequiredMatchingFolderId(root, envelope.AggregateId, out failureCode)
                || !TryReadRequiredIdentifier(root, "workspaceId", out workspaceId, out failureCode)
                || !TryReadRequiredIdentifier(root, "idempotencyKey", out acceptedIdempotencyKey, out failureCode)
                || !TryReadRequiredMatchingTaskId(root, expectedTaskId, out _, out failureCode))
            {
                return false;
            }

            if (!root.TryGetProperty("changes", out JsonElement changesElement)
                || changesElement.ValueKind != JsonValueKind.Array)
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            int changeCount = changesElement.GetArrayLength();
            if (changeCount is < 1 or > MaximumFileBatchItems)
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            List<TranslatedFileMutationItem> translated = new(changeCount);
            List<string> paths = new(changeCount);
            HashSet<string> operationIds = new(StringComparer.OrdinalIgnoreCase);
            long totalBytes = 0;

            foreach (JsonElement itemElement in changesElement.EnumerateArray())
            {
                if (!TryTranslateFileMutationItem(itemElement, out TranslatedFileMutationItem? item, out failureCode))
                {
                    return false;
                }

                string normalizedPath = item!.PathMetadata.NormalizedPath ?? string.Empty;
                if (!operationIds.Add(item.OperationId) || HasConflictingPath(paths, normalizedPath))
                {
                    failureCode = FolderResultCode.PathPolicyDenied;
                    return false;
                }

                if (item.FileOperationKind is "add" or "change")
                {
                    long itemBytes = item.ByteLength ?? -1;
                    if (itemBytes < 0 || totalBytes > MaximumBatchBytes - itemBytes)
                    {
                        failureCode = FolderResultCode.ValidationFailed;
                        return false;
                    }

                    totalBytes += itemBytes;
                }

                paths.Add(normalizedPath);
                translated.Add(item);
            }

            items = translated;
            failureCode = FolderResultCode.Accepted;
            return true;
        }
    }

    private static bool TryTranslateFileMutationItem(
        JsonElement item,
        out TranslatedFileMutationItem? translated,
        out FolderResultCode failureCode)
    {
        translated = null;
        if (item.ValueKind != JsonValueKind.Object)
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        if (!HasOnlyAllowedProperties(item, FileMutationItemProperties, allowNullProperty: "byteLength", out failureCode))
        {
            return false;
        }

        if (!TryReadRequiredInt64(item, "expectedFileVersion", out long expectedFileVersion, out failureCode)
            || expectedFileVersion < 0
            || !TryReadRequiredInt64(item, "expectedLockFence", out long expectedLockFence, out failureCode)
            || expectedLockFence < 1
            || !TryReadRequiredString(item, "fileOperationKind", out string? fileOperationKind, out failureCode)
            || !TryReadRequiredString(item, "transportOperation", out string? transportOperation, out failureCode)
            || !TryReadRequiredIdentifier(item, "operationId", out string? operationId, out failureCode)
            || !TryReadRequiredObject(item, "pathMetadata", out JsonElement pathElement, out failureCode)
            || !TryReadPathMetadata(pathElement, out FolderDomainProcessor.PathMetadataPayload? pathMetadata, out failureCode)
            || !TryReadOptionalString(item, "ownerFileId", out string? ownerFileId, out failureCode)
            || !TryReadOptionalString(item, "contentHashReference", out string? contentHashReference, out failureCode)
            || !TryReadNullableOptionalInt64(item, "byteLength", out long? byteLength, out failureCode))
        {
            if (failureCode == FolderResultCode.Accepted)
            {
                failureCode = FolderResultCode.ValidationFailed;
            }

            return false;
        }

        if (ownerFileId is not null && !FolderFileIdentityState.IsCanonical(ownerFileId))
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        bool hasInline = item.TryGetProperty("inlineContent", out JsonElement inlineElement);
        bool hasStream = item.TryGetProperty("streamDescriptor", out JsonElement streamElement);
        if ((hasInline && inlineElement.ValueKind != JsonValueKind.Object)
            || (hasStream && streamElement.ValueKind != JsonValueKind.Object))
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        WorkspacePathPolicyResult pathPolicy = WorkspacePathPolicyValidator.Validate(new AggPathMetadata(
            pathMetadata!.NormalizedPath ?? string.Empty,
            pathMetadata.DisplayName ?? string.Empty,
            pathMetadata.PathPolicyClass ?? string.Empty,
            pathMetadata.UnicodeNormalization ?? string.Empty));
        if (!pathPolicy.IsAccepted)
        {
            failureCode = FolderResultCode.PathPolicyDenied;
            return false;
        }

        if (string.Equals(fileOperationKind, "remove", StringComparison.Ordinal))
        {
            if (!string.Equals(transportOperation, "metadataOnlyRemoval", StringComparison.Ordinal)
                || contentHashReference is not null
                || byteLength is not null
                || hasInline
                || hasStream)
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            translated = new TranslatedFileMutationItem(
                operationId!,
                fileOperationKind!,
                transportOperation!,
                pathMetadata,
                ContentHashReference: null,
                ByteLength: null,
                MediaType: null,
                TransportEvidenceKind: null,
                ObservedByteLength: null,
                expectedFileVersion,
                expectedLockFence,
                ownerFileId,
                DecodedInlineBytes: null,
                StagingReference: null);
            failureCode = FolderResultCode.Accepted;
            return true;
        }

        if (fileOperationKind is not ("add" or "change")
            || contentHashReference is null
            || !ContentHashReferencePattern().IsMatch(contentHashReference)
            || byteLength is null
            || byteLength.Value is < 0 or > MaximumFileBytes)
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        if (string.Equals(transportOperation, "PutFileInline", StringComparison.Ordinal))
        {
            if (!hasInline || hasStream)
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            if (!HasOnlyAllowedProperties(inlineElement, PutFileInlineProperties, allowNullProperty: null, out failureCode)
                || !TryReadRequiredString(inlineElement, "mediaType", out string? mediaType, out failureCode)
                || !IsValidMediaType(mediaType)
                || !TryReadRequiredStringAllowEmpty(inlineElement, "contentBytes", out string? contentBytes, out failureCode)
                || !TryReadOptionalString(inlineElement, "contentMediaType", out string? contentMediaType, out failureCode))
            {
                if (failureCode == FolderResultCode.Accepted)
                {
                    failureCode = FolderResultCode.ValidationFailed;
                }

                return false;
            }

            if ((contentMediaType is not null && !IsValidMediaType(contentMediaType))
                || contentBytes!.Length > MaximumInlineBase64Length
                || HasWhitespace(contentBytes)
                || (contentBytes.Length == 0 && byteLength.Value != 0))
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(contentBytes);
            }
            catch (FormatException)
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            if (decoded.LongLength > InlineContentByteLimit
                || decoded.LongLength != byteLength.Value
                || !string.Equals(
                    "hashref_" + Convert.ToHexStringLower(SHA256.HashData(decoded)),
                    contentHashReference,
                    StringComparison.Ordinal))
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            translated = new TranslatedFileMutationItem(
                operationId!,
                fileOperationKind,
                transportOperation!,
                pathMetadata,
                contentHashReference,
                byteLength.Value,
                mediaType!,
                "inline_decoded",
                decoded.LongLength,
                expectedFileVersion,
                expectedLockFence,
                ownerFileId,
                decoded,
                StagingReference: null);
            failureCode = FolderResultCode.Accepted;
            return true;
        }

        if (string.Equals(transportOperation, "PutFileStream", StringComparison.Ordinal))
        {
            if (!hasStream || hasInline)
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            if (!HasOnlyAllowedProperties(streamElement, PutFileStreamProperties, allowNullProperty: null, out failureCode)
                || !TryReadRequiredString(streamElement, "mediaType", out string? mediaType, out failureCode)
                || !IsValidMediaType(mediaType)
                || !TryReadRequiredInt64(streamElement, "declaredLength", out long declaredLength, out failureCode)
                || !TryReadRequiredInt64(streamElement, "observedLength", out long observedLength, out failureCode)
                || !TryReadRequiredIdentifier(streamElement, "stagingReference", out string? stagingReference, out failureCode)
                || !TryReadRequiredString(streamElement, "observedContentHashReference", out string? observedHash, out failureCode)
                || !TryReadRequiredString(streamElement, "uploadMode", out string? uploadMode, out failureCode))
            {
                if (failureCode == FolderResultCode.Accepted)
                {
                    failureCode = FolderResultCode.ValidationFailed;
                }

                return false;
            }

            if (!string.Equals(uploadMode, "request_body_stream", StringComparison.Ordinal)
                || !string.Equals(observedHash, contentHashReference, StringComparison.Ordinal)
                || declaredLength is < StreamContentMinimumBytes or > MaximumFileBytes
                || observedLength is < StreamContentMinimumBytes or > MaximumFileBytes
                || declaredLength != byteLength.Value
                || observedLength != byteLength.Value)
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            translated = new TranslatedFileMutationItem(
                operationId!,
                fileOperationKind,
                transportOperation!,
                pathMetadata,
                contentHashReference,
                byteLength.Value,
                mediaType!,
                "stream_observed",
                observedLength,
                expectedFileVersion,
                expectedLockFence,
                ownerFileId,
                DecodedInlineBytes: null,
                stagingReference);
            failureCode = FolderResultCode.Accepted;
            return true;
        }

        failureCode = FolderResultCode.ValidationFailed;
        return false;
    }

    private static bool TryReadPathMetadata(
        JsonElement element,
        out FolderDomainProcessor.PathMetadataPayload? pathMetadata,
        out FolderResultCode failureCode)
    {
        pathMetadata = null;
        if (!HasOnlyAllowedProperties(element, PathMetadataProperties, allowNullProperty: null, out failureCode)
            || !TryReadRequiredString(element, "normalizedPath", out string? normalizedPath, out failureCode)
            || !TryReadRequiredString(element, "displayName", out string? displayName, out failureCode)
            || !TryReadRequiredString(element, "pathPolicyClass", out string? pathPolicyClass, out failureCode)
            || !TryReadRequiredString(element, "unicodeNormalization", out string? unicodeNormalization, out failureCode))
        {
            return false;
        }

        if (!ValidPathPolicyClasses.Contains(pathPolicyClass!)
            || !string.Equals(unicodeNormalization, "NFC", StringComparison.Ordinal))
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        pathMetadata = new FolderDomainProcessor.PathMetadataPayload(
            normalizedPath,
            displayName,
            pathPolicyClass,
            unicodeNormalization);
        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool TryReadFolderMetadata(
        JsonElement element,
        out string? displayName,
        out string? metadataClass,
        out FolderResultCode failureCode)
    {
        displayName = null;
        metadataClass = null;
        if (!HasOnlyAllowedProperties(element, FolderMetadataProperties, allowNullProperty: null, out failureCode)
            || !TryReadRequiredString(element, "displayName", out displayName, out failureCode)
            || !TryReadRequiredString(element, "metadataClass", out metadataClass, out failureCode))
        {
            return false;
        }

        if (displayName!.Length > 160 || !ValidMetadataTiers.Contains(metadataClass!))
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool TryTranslateBranchRefPolicyObject(
        JsonElement element,
        FrozenSet<string> allowedProperties,
        out FolderDomainProcessor.BranchRefPolicyPayload? policy,
        out FolderResultCode failureCode)
    {
        policy = null;
        if (!HasOnlyAllowedProperties(element, allowedProperties, allowNullProperty: null, out failureCode)
            || !TryReadRequiredIdentifier(element, "repositoryBindingId", out string? repositoryBindingId, out failureCode)
            || !TryReadRequiredIdentifier(element, "policyRef", out string? policyRef, out failureCode)
            || !TryReadRequiredString(element, "defaultRef", out string? defaultRef, out failureCode)
            || !TryReadRequiredStringArray(element, "allowedRefPatterns", out string[]? allowedRefPatterns, out failureCode)
            || !TryReadOptionalStringArray(element, "protectedRefPatterns", out string[]? protectedRefPatterns, out failureCode))
        {
            return false;
        }

        if (!FolderCommandValidator.IsValidBranchRefToken(defaultRef)
            || !FolderCommandValidator.AreValidBranchRefPatterns(allowedRefPatterns, required: true)
            || !FolderCommandValidator.AreValidBranchRefPatterns(protectedRefPatterns, required: false)
            || FolderCommandValidator.HasDuplicateBranchRefPatterns(allowedRefPatterns)
            || FolderCommandValidator.HasDuplicateBranchRefPatterns(protectedRefPatterns)
            || FolderCommandValidator.HasDuplicateBranchRefPatterns(allowedRefPatterns!, protectedRefPatterns))
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        policy = new FolderDomainProcessor.BranchRefPolicyPayload(
            DomainAcceptedSchemaVersion,
            repositoryBindingId,
            policyRef,
            defaultRef,
            allowedRefPatterns,
            protectedRefPatterns);
        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool TryParseDecoratedSubjectRef(
        string subjectRef,
        out string principalKindToken,
        out string principalId)
    {
        if (subjectRef.Contains(':', StringComparison.Ordinal))
        {
            return FolderAclContract.TryParseSubjectRef(subjectRef, out principalKindToken, out _, out principalId)
                && FolderCanonicalSegmentIdentifier.IsValid(principalId);
        }

        principalKindToken = "user";
        principalId = subjectRef;
        return FolderCanonicalSegmentIdentifier.IsValid(principalId);
    }

    private static bool TryParseRoot(
        ReadOnlyMemory<byte> rawPayload,
        FrozenSet<string> allowedProperties,
        out JsonDocument? document,
        out FolderResultCode failureCode)
    {
        document = null;
        try
        {
            document = JsonDocument.Parse(rawPayload);
        }
        catch (JsonException)
        {
            failureCode = FolderResultCode.MalformedJsonPayload;
            return false;
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            document = null;
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        if (!HasUniquePropertiesRecursive(document.RootElement))
        {
            document.Dispose();
            document = null;
            failureCode = FolderResultCode.MalformedJsonPayload;
            return false;
        }

        if (!HasOnlyAllowedProperties(document.RootElement, allowedProperties, allowNullProperty: null, out failureCode))
        {
            document.Dispose();
            document = null;
            return false;
        }

        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool HasUniquePropertiesRecursive(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name) || !HasUniquePropertiesRecursive(property.Value))
                {
                    return false;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (!HasUniquePropertiesRecursive(item))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasOnlyAllowedProperties(
        JsonElement obj,
        FrozenSet<string> allowedProperties,
        string? allowNullProperty,
        out FolderResultCode failureCode)
    {
        foreach (JsonProperty property in obj.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                failureCode = FolderResultCode.MalformedJsonPayload;
                return false;
            }

            if (property.Value.ValueKind == JsonValueKind.Null
                && !string.Equals(property.Name, allowNullProperty, StringComparison.Ordinal))
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }
        }

        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool TryReadRequiredString(
        JsonElement obj,
        string propertyName,
        out string? value,
        out FolderResultCode failureCode)
    {
        value = null;
        if (!obj.TryGetProperty(propertyName, out JsonElement property))
        {
            failureCode = FolderResultCode.MalformedJsonPayload;
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        string? candidate = property.GetString();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        value = candidate;
        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool TryReadRequiredStringAllowEmpty(
        JsonElement obj,
        string propertyName,
        out string? value,
        out FolderResultCode failureCode)
    {
        value = null;
        if (!obj.TryGetProperty(propertyName, out JsonElement property))
        {
            failureCode = FolderResultCode.MalformedJsonPayload;
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        value = property.GetString() ?? string.Empty;
        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool TryReadOptionalString(
        JsonElement obj,
        string propertyName,
        out string? value,
        out FolderResultCode failureCode)
    {
        value = null;
        if (!obj.TryGetProperty(propertyName, out JsonElement property))
        {
            failureCode = FolderResultCode.Accepted;
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        string? candidate = property.GetString();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        value = candidate;
        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool TryReadRequiredIdentifier(
        JsonElement obj,
        string propertyName,
        out string? value,
        out FolderResultCode failureCode)
    {
        if (!TryReadRequiredString(obj, propertyName, out value, out failureCode))
        {
            return false;
        }

        if (!FolderCanonicalSegmentIdentifier.IsValid(value))
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool TryReadOptionalIdentifier(
        JsonElement obj,
        string propertyName,
        out string? value,
        out FolderResultCode failureCode)
    {
        if (!TryReadOptionalString(obj, propertyName, out value, out failureCode))
        {
            return false;
        }

        if (value is not null && !FolderCanonicalSegmentIdentifier.IsValid(value))
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool TryReadRequiredMatchingFolderId(
        JsonElement obj,
        string expectedAggregateId,
        out FolderResultCode failureCode)
    {
        if (!TryReadRequiredIdentifier(obj, "folderId", out string? folderId, out failureCode))
        {
            return false;
        }

        if (!string.Equals(folderId, expectedAggregateId, StringComparison.Ordinal))
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool TryReadRequiredMatchingTaskId(
        JsonElement obj,
        string expectedTaskId,
        out string? taskId,
        out FolderResultCode failureCode)
    {
        if (!TryReadRequiredIdentifier(obj, "taskId", out taskId, out failureCode))
        {
            return false;
        }

        if (!string.Equals(taskId, expectedTaskId, StringComparison.Ordinal))
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool TryReadRequiredIdempotencyKey(
        JsonElement obj,
        out FolderResultCode failureCode)
        => TryReadRequiredIdentifier(obj, "idempotencyKey", out _, out failureCode);

    private static bool TryReadRequiredInt32(
        JsonElement obj,
        string propertyName,
        out int value,
        out FolderResultCode failureCode)
    {
        value = 0;
        if (!obj.TryGetProperty(propertyName, out JsonElement property))
        {
            failureCode = FolderResultCode.MalformedJsonPayload;
            return false;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out value))
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool TryReadRequiredInt64(
        JsonElement obj,
        string propertyName,
        out long value,
        out FolderResultCode failureCode)
    {
        value = 0;
        if (!obj.TryGetProperty(propertyName, out JsonElement property))
        {
            failureCode = FolderResultCode.MalformedJsonPayload;
            return false;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt64(out value))
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool TryReadNullableOptionalInt64(
        JsonElement obj,
        string propertyName,
        out long? value,
        out FolderResultCode failureCode)
    {
        value = null;
        if (!obj.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
        {
            failureCode = FolderResultCode.Accepted;
            return true;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt64(out long parsed))
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        value = parsed;
        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool TryReadRequiredObject(
        JsonElement obj,
        string propertyName,
        out JsonElement value,
        out FolderResultCode failureCode)
    {
        value = default;
        if (!obj.TryGetProperty(propertyName, out JsonElement property))
        {
            failureCode = FolderResultCode.MalformedJsonPayload;
            return false;
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        value = property;
        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool TryReadRequiredStringArray(
        JsonElement obj,
        string propertyName,
        out string[]? values,
        out FolderResultCode failureCode)
    {
        values = null;
        if (!obj.TryGetProperty(propertyName, out JsonElement property))
        {
            failureCode = FolderResultCode.MalformedJsonPayload;
            return false;
        }

        return TryReadStringArrayElement(property, out values, out failureCode);
    }

    private static bool TryReadOptionalStringArray(
        JsonElement obj,
        string propertyName,
        out string[]? values,
        out FolderResultCode failureCode)
    {
        values = null;
        if (!obj.TryGetProperty(propertyName, out JsonElement property))
        {
            failureCode = FolderResultCode.Accepted;
            return true;
        }

        return TryReadStringArrayElement(property, out values, out failureCode);
    }

    private static bool TryReadStringArrayElement(
        JsonElement property,
        out string[]? values,
        out FolderResultCode failureCode)
    {
        values = null;
        if (property.ValueKind != JsonValueKind.Array)
        {
            failureCode = FolderResultCode.ValidationFailed;
            return false;
        }

        List<string> list = new(property.GetArrayLength());
        foreach (JsonElement item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            string? text = item.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                failureCode = FolderResultCode.ValidationFailed;
                return false;
            }

            list.Add(text);
        }

        values = [.. list];
        failureCode = FolderResultCode.Accepted;
        return true;
    }

    private static bool HasConflictingPath(List<string> existingPaths, string path)
    {
        foreach (string existing in existingPaths)
        {
            if (existing.Equals(path, StringComparison.OrdinalIgnoreCase)
                || existing.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(existing + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string[] left = existing.Split('/');
            string[] right = path.Split('/');
            for (int index = 0; index < Math.Min(left.Length, right.Length); index++)
            {
                if (!left[index].Equals(right[index], StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsValidMediaType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return false;
        }

        int slash = value.IndexOf('/');
        return slash > 0
            && slash < value.Length - 1
            && IsMediaToken(value[..slash])
            && IsMediaToken(value[(slash + 1)..]);
    }

    private static bool IsMediaToken(string value)
    {
        if (value.Length == 0 || !char.IsAsciiLetterOrDigit(value[0]))
        {
            return false;
        }

        foreach (char c in value)
        {
            bool accepted = char.IsAsciiLetterOrDigit(c)
                || c is '!' or '#' or '$' or '&' or '^' or '_' or '.' or '+' or '-';
            if (!accepted)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasWhitespace(string value)
    {
        foreach (char c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                return true;
            }
        }

        return false;
    }

    private static FrozenSet<string> BuildAuditMetadataKeySet()
    {
        HashSet<string> values = new(StringComparer.Ordinal);
        foreach (FieldInfo field in typeof(AuditMetadataKey).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            JsonStringEnumMemberNameAttribute? attr = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>();
            if (attr is not null)
            {
                values.Add(attr.Name);
            }
        }

        return values.ToFrozenSet(StringComparer.Ordinal);
    }

    [GeneratedRegex("\\Ahashref_[A-Za-z0-9]{32,96}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ContentHashReferencePattern();
}
