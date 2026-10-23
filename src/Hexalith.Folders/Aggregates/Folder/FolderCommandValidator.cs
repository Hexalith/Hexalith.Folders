using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Hashing;

namespace Hexalith.Folders.Aggregates.Folder;

public static partial class FolderCommandValidator
{
    internal const int MaxIdentifierLength = FolderStreamName.MaxSegmentLength;
    internal const int MaxTagCount = 32;
    internal const int MaxBranchRefPatternCount = 16;
    private const int MaxDisplayNameLength = 128;
    private const int MaxDescriptionLength = 512;
    private const int MinimumWorkspaceLockLeaseSeconds = 1;
    private const int MaximumWorkspaceLockLeaseSeconds = 86400;

    // Two-tier blocklist: substring terms catch payload-shaped leakage (URLs, paths, mail
    // addresses, diff bodies) inside free-form metadata fields like DisplayName/Description;
    // word terms catch identifier-shaped tokens (`branch`, `auth`, `display`) that surface
    // inside opaque identifiers and are matched only at lower-snake-case word boundaries.
    // Substring matching identifier-shaped terms would silently null valid tenant IDs such
    // as `tenant-authority` or `acme-display-prod` from result echoes.
    private static readonly string[] ForbiddenMetadataSubstrings =
    [
        "credential",
        "token",
        "secret",
        "repository",
        "repo-",
        "repo_",
        "raw file",
        "file content",
        "diff --git",
        "generated context",
        "provider payload",
        "synthetic",
        "unauthorized",
        "@",
        "://",
        "\\",
        "/",
        "|",
    ];

    // Identifier-only blocklist: matched at whole-word boundaries inside lower-snake-case
    // canonical identifiers (split on `-`, `_`, `.`). A term is "present" only if it equals
    // a full segment, not if it appears as part of a larger word.
    private static readonly string[] ForbiddenMetadataWordTerms =
    [
        "branch",
        "email",
        "auth",
        "display",
    ];

    public static FolderCommandValidationResult Validate(IFolderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (FolderStreamName.IsReservedSystemTenant(command.ManagedTenantId))
        {
            return FolderCommandValidationResult.Rejected(FolderResultCode.ReservedTenant);
        }

        if (!FolderStreamName.IsValidSegment(command.ManagedTenantId))
        {
            return FolderCommandValidationResult.Rejected(FolderResultCode.InvalidTenant);
        }

        if (!FolderStreamName.IsValidSegment(command.OrganizationId)
            || !FolderStreamName.IsValidSegment(command.FolderId))
        {
            return FolderCommandValidationResult.Rejected(FolderResultCode.InvalidFolderId);
        }

        if (!string.Equals(command.OrganizationId, command.ManagedTenantId, StringComparison.Ordinal))
        {
            return FolderCommandValidationResult.Rejected(FolderResultCode.TenantMismatch);
        }

        if (!FolderStreamName.TryCreate(command.ManagedTenantId, command.FolderId,
            out _, out FolderResultCode streamCode))
        {
            return FolderCommandValidationResult.Rejected(streamCode);
        }

        if (!IsSafeEvidenceIdentifier(command.ActorPrincipalId)
            || !IsSafeEvidenceIdentifier(command.CorrelationId)
            || !IsSafeEvidenceIdentifier(command.TaskId)
            || !IsSafeEvidenceIdentifier(command.IdempotencyKey))
        {
            return FolderCommandValidationResult.Rejected(FolderResultCode.MalformedEvidence);
        }

        if (command is IFolderAccessCommand access)
        {
            IReadOnlyList<FolderAccessOperation>? operations = ValidateAndCanonicalizeAccessOperations(access, out FolderResultCode code);
            return operations is null
                ? FolderCommandValidationResult.Rejected(code)
                : FolderCommandValidationResult.AcceptedAccess(Fingerprint(access, operations), operations);
        }

        if (command is ArchiveFolder archive)
        {
            if (!string.Equals(archive.RequestSchemaVersion, "v1", StringComparison.Ordinal)
                || !TryParseArchiveReasonCode(archive.ArchiveReasonCode, out FolderArchiveReasonCode archiveReasonCode))
            {
                return FolderCommandValidationResult.Rejected(FolderResultCode.ValidationFailed);
            }

            return FolderCommandValidationResult.AcceptedArchive(
                Fingerprint(archive, archiveReasonCode),
                archiveReasonCode);
        }

        if (command is CreateRepositoryBackedFolder repositoryBacked)
        {
            if (!string.Equals(repositoryBacked.RequestSchemaVersion, "v1", StringComparison.Ordinal)
                || !IsValidIdentifier(repositoryBacked.RepositoryBindingId)
                || !IsValidIdentifier(repositoryBacked.ProviderBindingRef)
                || !IsValidIdentifier(repositoryBacked.RepositoryProfileRef)
                || !IsValidIdentifier(repositoryBacked.BranchRefPolicyRef)
                || !IsValidIdentifier(repositoryBacked.CredentialScopeClass)
                || !IsSafeMetadata(repositoryBacked.FolderMetadataDisplayName, required: true, MaxDisplayNameLength))
            {
                return FolderCommandValidationResult.Rejected(FolderResultCode.ValidationFailed);
            }

            return FolderCommandValidationResult.AcceptedRepositoryBinding(Fingerprint(repositoryBacked));
        }

        if (command is BindRepository bindRepository)
        {
            if (!string.Equals(bindRepository.RequestSchemaVersion, "v1", StringComparison.Ordinal)
                || !IsValidIdentifier(bindRepository.RepositoryBindingId)
                || !IsValidIdentifier(bindRepository.ProviderBindingRef)
                || !IsValidIdentifier(bindRepository.ExternalRepositoryRef)
                || !IsValidIdentifier(bindRepository.BranchRefPolicyRef)
                || !IsValidIdentifier(bindRepository.CredentialScopeClass))
            {
                return FolderCommandValidationResult.Rejected(FolderResultCode.ValidationFailed);
            }

            return FolderCommandValidationResult.AcceptedRepositoryBinding(Fingerprint(bindRepository));
        }

        if (command is ConfigureBranchRefPolicy branchRefPolicy)
        {
            if (!string.Equals(branchRefPolicy.RequestSchemaVersion, "v1", StringComparison.Ordinal)
                || !IsValidIdentifier(branchRefPolicy.RepositoryBindingId)
                || !IsValidIdentifier(branchRefPolicy.PolicyRef)
                || !IsValidBranchRefToken(branchRefPolicy.DefaultRef)
                || !AreValidBranchRefPatterns(branchRefPolicy.AllowedRefPatterns, required: true)
                || !AreValidBranchRefPatterns(branchRefPolicy.ProtectedRefPatterns, required: false)
                || HasDuplicateBranchRefPatterns(branchRefPolicy.AllowedRefPatterns)
                || HasDuplicateBranchRefPatterns(branchRefPolicy.ProtectedRefPatterns)
                || HasDuplicateBranchRefPatterns(branchRefPolicy.AllowedRefPatterns, branchRefPolicy.ProtectedRefPatterns))
            {
                return FolderCommandValidationResult.Rejected(FolderResultCode.ValidationFailed);
            }

            return FolderCommandValidationResult.AcceptedRepositoryBinding(Fingerprint(branchRefPolicy));
        }

        if (command is PrepareWorkspace prepareWorkspace)
        {
            if (!string.Equals(prepareWorkspace.RequestSchemaVersion, "v1", StringComparison.Ordinal)
                || !IsValidIdentifier(prepareWorkspace.WorkspaceId)
                || !IsValidIdentifier(prepareWorkspace.RepositoryBindingId)
                || !IsValidIdentifier(prepareWorkspace.BranchRefPolicyRef)
                || !IsValidIdentifier(prepareWorkspace.WorkspacePolicyRef))
            {
                return FolderCommandValidationResult.Rejected(FolderResultCode.ValidationFailed);
            }

            return FolderCommandValidationResult.AcceptedRepositoryBinding(Fingerprint(prepareWorkspace));
        }

        if (command is LockWorkspace lockWorkspace)
        {
            if (!string.Equals(lockWorkspace.RequestSchemaVersion, "v1", StringComparison.Ordinal)
                || !string.Equals(lockWorkspace.LockIntent, "exclusive_write", StringComparison.Ordinal)
                || !IsValidIdentifier(lockWorkspace.WorkspaceId)
                || lockWorkspace.RequestedLeaseSeconds is < MinimumWorkspaceLockLeaseSeconds or > MaximumWorkspaceLockLeaseSeconds)
            {
                return FolderCommandValidationResult.Rejected(FolderResultCode.ValidationFailed);
            }

            return FolderCommandValidationResult.AcceptedRepositoryBinding(Fingerprint(lockWorkspace));
        }

        if (command is ReleaseWorkspaceLock releaseWorkspaceLock)
        {
            if (!string.Equals(releaseWorkspaceLock.RequestSchemaVersion, "v1", StringComparison.Ordinal)
                || !IsValidIdentifier(releaseWorkspaceLock.WorkspaceId)
                || !IsValidIdentifier(releaseWorkspaceLock.LockId)
                || !IsValidIdentifier(releaseWorkspaceLock.LockOwnershipProof)
                || !IsValidReleaseReasonCode(releaseWorkspaceLock.ReleaseReasonCode))
            {
                return FolderCommandValidationResult.Rejected(FolderResultCode.ValidationFailed);
            }

            return FolderCommandValidationResult.AcceptedRepositoryBinding(Fingerprint(releaseWorkspaceLock));
        }

        if (command is MutateWorkspaceFile mutateWorkspaceFile)
        {
            if (!IsValidFileMutationCommand(mutateWorkspaceFile))
            {
                return FolderCommandValidationResult.Rejected(FolderResultCode.ValidationFailed);
            }

            WorkspacePathPolicyResult pathPolicy = WorkspacePathPolicyValidator.Validate(mutateWorkspaceFile.PathMetadata);
            if (!pathPolicy.IsAccepted)
            {
                return FolderCommandValidationResult.Rejected(FolderResultCode.PathPolicyDenied);
            }

            return FolderCommandValidationResult.AcceptedRepositoryBinding(Fingerprint(mutateWorkspaceFile));
        }

        if (command is CommitWorkspace commitWorkspace)
        {
            if (!IsValidCommitWorkspaceCommand(commitWorkspace))
            {
                return FolderCommandValidationResult.Rejected(FolderResultCode.ValidationFailed);
            }

            return FolderCommandValidationResult.AcceptedRepositoryBinding(Fingerprint(commitWorkspace));
        }

        if (command is not CreateFolder create)
        {
            return FolderCommandValidationResult.Rejected(FolderResultCode.ValidationFailed);
        }

        IReadOnlyList<string>? tags = ValidateAndCanonicalizeTags(create.Tags);
        if (!IsSafeMetadata(create.DisplayName, required: true, MaxDisplayNameLength)
            || !IsSafeMetadata(create.Description, required: false, MaxDescriptionLength)
            || !IsSafePathLabel(create.PathLabel)
            || tags is null)
        {
            return FolderCommandValidationResult.Rejected(FolderResultCode.InvalidFolderMetadata);
        }

        return FolderCommandValidationResult.Accepted(Fingerprint(create, tags), tags);
    }

    internal static bool IsValidIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= MaxIdentifierLength
            && (CanonicalIdentifierPattern().IsMatch(value)
                || PreauthorizedRequestContext.IsCandidateOpaqueIdentifier(value));

    internal static string CanonicalMetadata(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Normalize(NormalizationForm.FormC).ToLower(CultureInfo.InvariantCulture);

    // Produces a SHA-256 hex digest over length-prefixed UTF-8 fields. Length-prefixing
    // prevents field-separator smuggling (no field can collide with another by shifting
    // bytes across a delimiter) and the digest caps width at 64 chars regardless of input.
    private static string Fingerprint(CreateFolder command, IReadOnlyList<string> tags)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        LengthPrefixedHash.AppendField(hash, command.CommandType);
        LengthPrefixedHash.AppendField(hash, command.ManagedTenantId);
        LengthPrefixedHash.AppendField(hash, command.OrganizationId);
        LengthPrefixedHash.AppendField(hash, command.FolderId);
        LengthPrefixedHash.AppendField(hash, CanonicalMetadata(command.DisplayName));
        LengthPrefixedHash.AppendField(hash, CanonicalMetadata(command.Description));
        LengthPrefixedHash.AppendField(hash, CanonicalMetadata(command.PathLabel));
        LengthPrefixedHash.AppendInt32(hash, tags.Count);
        foreach (string tag in tags)
        {
            LengthPrefixedHash.AppendField(hash, tag);
        }

        LengthPrefixedHash.AppendField(hash, command.ActorPrincipalId);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Fingerprint(ArchiveFolder command, FolderArchiveReasonCode archiveReasonCode)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        LengthPrefixedHash.AppendField(hash, command.CommandType);
        LengthPrefixedHash.AppendField(hash, command.ManagedTenantId);
        LengthPrefixedHash.AppendField(hash, command.OrganizationId);
        LengthPrefixedHash.AppendField(hash, command.FolderId);
        LengthPrefixedHash.AppendField(hash, command.RequestSchemaVersion);
        LengthPrefixedHash.AppendField(hash, ToContractValue(archiveReasonCode));
        LengthPrefixedHash.AppendField(hash, command.ActorPrincipalId);
        LengthPrefixedHash.AppendField(hash, command.CorrelationId);
        LengthPrefixedHash.AppendField(hash, command.TaskId);
        LengthPrefixedHash.AppendField(hash, command.IdempotencyKey);
        LengthPrefixedHash.AppendField(hash, command.PayloadTenantId);
        LengthPrefixedHash.AppendInt32(hash, command.ClientControlledTenantIds.Count);
        foreach (KeyValuePair<string, string?> tenantEntry in command.ClientControlledTenantIds.OrderBy(static x => x.Key, StringComparer.Ordinal))
        {
            LengthPrefixedHash.AppendField(hash, tenantEntry.Key);
            LengthPrefixedHash.AppendField(hash, tenantEntry.Value);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    internal static string BindArchiveDecisionFingerprint(
        ArchiveFolder command,
        string commandFingerprint,
        string? policyVersion,
        string? freshnessWatermark)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandFingerprint);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        LengthPrefixedHash.AppendField(hash, "archive-decision-v1");
        LengthPrefixedHash.AppendField(hash, commandFingerprint);
        LengthPrefixedHash.AppendField(hash, command.IdempotencyKey);
        LengthPrefixedHash.AppendField(hash, policyVersion);
        LengthPrefixedHash.AppendField(hash, freshnessWatermark);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Fingerprint(CreateRepositoryBackedFolder command)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        LengthPrefixedHash.AppendField(hash, command.CommandType);
        LengthPrefixedHash.AppendField(hash, command.ManagedTenantId);
        LengthPrefixedHash.AppendField(hash, command.OrganizationId);
        LengthPrefixedHash.AppendField(hash, command.FolderId);
        LengthPrefixedHash.AppendField(hash, command.RequestSchemaVersion);
        LengthPrefixedHash.AppendField(hash, command.CredentialScopeClass);
        LengthPrefixedHash.AppendField(hash, CanonicalMetadata(command.FolderMetadataDisplayName));
        LengthPrefixedHash.AppendField(hash, command.ProviderBindingRef);
        LengthPrefixedHash.AppendField(hash, command.RepositoryBindingId);
        LengthPrefixedHash.AppendField(hash, command.RepositoryProfileRef);
        LengthPrefixedHash.AppendField(hash, command.BranchRefPolicyRef);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    internal static string DeriveRepositoryBindingId(
        string managedTenantId,
        string folderId,
        string providerBindingRef,
        string externalRepositoryRef,
        string branchRefPolicyRef)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        LengthPrefixedHash.AppendField(hash, "repository-binding-id-v1");
        LengthPrefixedHash.AppendField(hash, managedTenantId);
        LengthPrefixedHash.AppendField(hash, folderId);
        LengthPrefixedHash.AppendField(hash, providerBindingRef);
        LengthPrefixedHash.AppendField(hash, externalRepositoryRef);
        LengthPrefixedHash.AppendField(hash, branchRefPolicyRef);

        string digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        return $"repository_binding_{digest[..32]}";
    }

    internal static string ExternalRepositoryRefFingerprint(BindRepository command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ExternalRepositoryRefFingerprint(command.ManagedTenantId, command.ProviderBindingRef, command.ExternalRepositoryRef);
    }

    internal static string ExternalRepositoryRefFingerprint(string tenantId, string bindingRef, string externalRef)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        LengthPrefixedHash.AppendField(hash, "external-repository-ref-v1");
        LengthPrefixedHash.AppendField(hash, tenantId);
        LengthPrefixedHash.AppendField(hash, bindingRef);
        LengthPrefixedHash.AppendField(hash, externalRef);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Fingerprint(BindRepository command)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        LengthPrefixedHash.AppendField(hash, command.CommandType);
        LengthPrefixedHash.AppendField(hash, command.ManagedTenantId);
        LengthPrefixedHash.AppendField(hash, command.OrganizationId);
        LengthPrefixedHash.AppendField(hash, command.FolderId);
        LengthPrefixedHash.AppendField(hash, command.RequestSchemaVersion);
        LengthPrefixedHash.AppendField(hash, command.BranchRefPolicyRef);
        LengthPrefixedHash.AppendField(hash, command.CredentialScopeClass);
        LengthPrefixedHash.AppendField(hash, command.ExternalRepositoryRef);
        LengthPrefixedHash.AppendField(hash, command.ProviderBindingRef);
        LengthPrefixedHash.AppendField(hash, command.RepositoryBindingId);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Fingerprint(ConfigureBranchRefPolicy command)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        LengthPrefixedHash.AppendInt32(hash, command.AllowedRefPatterns.Count);
        foreach (string pattern in command.AllowedRefPatterns)
        {
            LengthPrefixedHash.AppendField(hash, pattern);
        }

        LengthPrefixedHash.AppendField(hash, command.DefaultRef);
        LengthPrefixedHash.AppendField(hash, command.PolicyRef);
        LengthPrefixedHash.AppendInt32(hash, command.ProtectedRefPatterns?.Count ?? -1);
        if (command.ProtectedRefPatterns is not null)
        {
            foreach (string pattern in command.ProtectedRefPatterns)
            {
                LengthPrefixedHash.AppendField(hash, pattern);
            }
        }

        LengthPrefixedHash.AppendField(hash, command.FolderId);
        LengthPrefixedHash.AppendField(hash, command.RepositoryBindingId);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Fingerprint(PrepareWorkspace command)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        LengthPrefixedHash.AppendField(hash, command.BranchRefPolicyRef);
        LengthPrefixedHash.AppendField(hash, command.FolderId);
        LengthPrefixedHash.AppendField(hash, command.RepositoryBindingId);
        LengthPrefixedHash.AppendField(hash, command.TaskId);
        LengthPrefixedHash.AppendField(hash, command.WorkspaceId);
        LengthPrefixedHash.AppendField(hash, command.WorkspacePolicyRef);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Fingerprint(LockWorkspace command)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        LengthPrefixedHash.AppendField(hash, command.FolderId);
        LengthPrefixedHash.AppendField(hash, command.LockIntent);
        LengthPrefixedHash.AppendInt32(hash, command.RequestedLeaseSeconds);
        LengthPrefixedHash.AppendField(hash, command.TaskId);
        LengthPrefixedHash.AppendField(hash, command.WorkspaceId);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    internal static string DeriveWorkspaceLockId(LockWorkspace command, string idempotencyFingerprint)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyFingerprint);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        LengthPrefixedHash.AppendField(hash, "workspace-lock-id-v1");
        LengthPrefixedHash.AppendField(hash, command.ManagedTenantId);
        LengthPrefixedHash.AppendField(hash, command.FolderId);
        LengthPrefixedHash.AppendField(hash, command.WorkspaceId);
        LengthPrefixedHash.AppendField(hash, command.TaskId);
        LengthPrefixedHash.AppendField(hash, idempotencyFingerprint);

        string digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        return $"workspace_lock_{digest[..32]}";
    }

    internal static string DeriveWorkspaceLockOwnershipProof(
        string managedTenantId,
        string folderId,
        string workspaceId,
        string taskId,
        string lockId)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        LengthPrefixedHash.AppendField(hash, "workspace-lock-ownership-proof-v1");
        LengthPrefixedHash.AppendField(hash, managedTenantId);
        LengthPrefixedHash.AppendField(hash, folderId);
        LengthPrefixedHash.AppendField(hash, workspaceId);
        LengthPrefixedHash.AppendField(hash, taskId);
        LengthPrefixedHash.AppendField(hash, lockId);

        string digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        return $"lock_proof_{digest[..32]}";
    }

    private static string Fingerprint(ReleaseWorkspaceLock command)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        LengthPrefixedHash.AppendField(hash, command.FolderId);
        LengthPrefixedHash.AppendField(hash, command.LockId);
        LengthPrefixedHash.AppendField(hash, command.LockOwnershipProof);
        LengthPrefixedHash.AppendField(hash, command.TaskId);
        LengthPrefixedHash.AppendField(hash, command.WorkspaceId);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Fingerprint(MutateWorkspaceFile command)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        LengthPrefixedHash.AppendField(hash, ToFileMutationOperationId(command.FileOperationKind));
        if (command.FileOperationKind is "add" or "change")
        {
            LengthPrefixedHash.AppendField(hash, command.ContentHashReference);
        }

        LengthPrefixedHash.AppendField(hash, command.FileOperationKind);
        LengthPrefixedHash.AppendField(hash, command.OperationId);
        LengthPrefixedHash.AppendField(hash, WorkspacePathPolicyValidator.CanonicalPathMetadataFingerprint(command.PathMetadata));
        LengthPrefixedHash.AppendField(hash, command.PathMetadata.PathPolicyClass);
        LengthPrefixedHash.AppendField(hash, command.TaskId);
        LengthPrefixedHash.AppendField(hash, command.WorkspaceId);

        if (command.ExpectedLockFence is { } fence) LengthPrefixedHash.AppendField(hash, fence.ToString(CultureInfo.InvariantCulture));
        if (command.ExpectedFileVersion is { } version) LengthPrefixedHash.AppendField(hash, version.ToString(CultureInfo.InvariantCulture));
        if (command.OwnerFileId is not null && command.FileOperationKind != "add") LengthPrefixedHash.AppendField(hash, command.OwnerFileId);
        if (command.ProjectFileReferenceId is not null) LengthPrefixedHash.AppendField(hash, command.ProjectFileReferenceId);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Fingerprint(CommitWorkspace command)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        LengthPrefixedHash.AppendField(hash, command.CommandType);
        LengthPrefixedHash.AppendField(hash, command.AuthorMetadataReference);
        LengthPrefixedHash.AppendField(hash, command.BranchRefTarget);
        LengthPrefixedHash.AppendField(hash, command.ChangedPathMetadataDigest);
        LengthPrefixedHash.AppendField(hash, command.CommitMessageClassification);
        LengthPrefixedHash.AppendField(hash, command.OperationId);
        LengthPrefixedHash.AppendField(hash, command.TaskId);
        LengthPrefixedHash.AppendField(hash, command.WorkspaceId);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static bool IsValidCommitWorkspaceCommand(CommitWorkspace command)
        => string.Equals(command.RequestSchemaVersion, "v1", StringComparison.Ordinal)
        && IsValidIdentifier(command.WorkspaceId)
        && IsValidIdentifier(command.OperationId)
        && IsValidAuthorMetadataReference(command.AuthorMetadataReference)
        && IsValidBranchRefTarget(command.BranchRefTarget)
        && IsValidCommitMessageClassification(command.CommitMessageClassification)
        && IsValidChangedPathMetadataDigest(command.ChangedPathMetadataDigest);

    internal static bool IsValidCommitReference(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 16 and <= MaxIdentifierLength
        && CommitReferencePattern().IsMatch(value);

    internal static bool IsValidCommitReconciliationReference(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 16 and <= MaxIdentifierLength
        && CommitReconciliationReferencePattern().IsMatch(value);

    internal static bool IsValidCommitOutcomeCategory(string? value)
        => IsValidLowerSnakeMetadata(value, 80);

    internal static string DeriveCommitReconciliationReference(CommitWorkspace command)
    {
        ArgumentNullException.ThrowIfNull(command);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        LengthPrefixedHash.AppendField(hash, "commit-reconciliation-v1");
        LengthPrefixedHash.AppendField(hash, command.ManagedTenantId);
        LengthPrefixedHash.AppendField(hash, command.FolderId);
        LengthPrefixedHash.AppendField(hash, command.WorkspaceId);
        LengthPrefixedHash.AppendField(hash, command.OperationId);
        LengthPrefixedHash.AppendField(hash, command.CorrelationId);
        LengthPrefixedHash.AppendField(hash, command.TaskId);

        string digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        return $"reconcile_{digest[..32]}";
    }

    private static bool IsValidAuthorMetadataReference(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 16 and <= MaxIdentifierLength
        && AuthorMetadataReferencePattern().IsMatch(value);

    private static bool IsValidBranchRefTarget(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 16 and <= MaxIdentifierLength
        && BranchRefTargetPattern().IsMatch(value);

    private static bool IsValidChangedPathMetadataDigest(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 16 and <= MaxIdentifierLength
        && ChangedPathMetadataDigestPattern().IsMatch(value);

    private static bool IsValidCommitMessageClassification(string? value)
        => IsValidLowerSnakeMetadata(value, 80);

    private static bool IsValidLowerSnakeMetadata(string? value, int maxLength)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= maxLength
        && LowerSnakeMetadataPattern().IsMatch(value);

    private static bool IsValidFileMutationCommand(MutateWorkspaceFile command)
    {
        if (!string.Equals(command.RequestSchemaVersion, "v1", StringComparison.Ordinal)
            || !IsValidIdentifier(command.WorkspaceId)
            || !IsValidIdentifier(command.OperationId))
        {
            return false;
        }

        if (command.OwnerFileId is not null && !FolderFileIdentityState.IsCanonical(command.OwnerFileId)
            || command.ProjectFileReferenceId is not null && (!FolderFileIdentityState.IsProjectReference(command.ProjectFileReferenceId)
                || command.OwnerFileId is null)) return false;

        return command.FileOperationKind switch
        {
            "add" or "change" => IsValidAddOrChangeMutation(command),
            "remove" => string.Equals(command.TransportOperation, "metadataOnlyRemoval", StringComparison.Ordinal)
                && command.ContentHashReference is null
                && command.ByteLength is null
                && command.MediaType is null
                && command.TransportEvidenceKind is null
                && command.ObservedByteLength is null,
            _ => false,
        };
    }

    private static bool IsValidAddOrChangeMutation(MutateWorkspaceFile command)
        => IsValidIdentifier(command.ContentHashReference)
        && IsValidMediaType(command.MediaType)
        && IsValidTransportEvidenceKind(command.TransportEvidenceKind, command.TransportOperation)
        && command.TransportOperation is "PutFileInline" or "PutFileStream"
        && command.ByteLength is not null
        && command.ObservedByteLength == command.ByteLength
        && (string.Equals(command.TransportOperation, "PutFileInline", StringComparison.Ordinal)
            ? command.ByteLength is >= 0 and <= 262144
            : command.ByteLength >= 262145);

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
        foreach (char c in value)
        {
            bool accepted = char.IsAsciiLetterOrDigit(c)
                || c is '!' or '#' or '$' or '&' or '^' or '_' or '.' or '+' or '-';
            if (!accepted)
            {
                return false;
            }
        }

        return value.Length > 0;
    }

    private static bool IsValidTransportEvidenceKind(string? value, string transportOperation)
        => string.Equals(transportOperation, "PutFileInline", StringComparison.Ordinal)
            ? string.Equals(value, "inline_decoded", StringComparison.Ordinal)
            : string.Equals(value, "stream_observed", StringComparison.Ordinal);

    private static string ToFileMutationOperationId(string fileOperationKind)
        => fileOperationKind switch
        {
            "add" => "AddFile",
            "change" => "ChangeFile",
            "remove" => "RemoveFile",
            _ => string.Empty,
        };

    private static bool IsValidReleaseReasonCode(string? value)
        => value is "caller_completed"
            or "caller_abandoned"
            or "operator_requested"
            or "authorization_revoked"
            or "task_cancelled"
            or "lock_revoked";

    private static string Fingerprint(IFolderAccessCommand command, IReadOnlyList<FolderAccessOperation> operations)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        LengthPrefixedHash.AppendField(hash, command.CommandType);
        LengthPrefixedHash.AppendField(hash, command.ManagedTenantId);
        LengthPrefixedHash.AppendField(hash, command.OrganizationId);
        LengthPrefixedHash.AppendField(hash, command.FolderId);
        LengthPrefixedHash.AppendField(hash, command.ActorPrincipalId);
        LengthPrefixedHash.AppendInt32(hash, operations.Count);
        foreach (FolderAccessOperation operation in operations)
        {
            FolderAccessEntryKey key = new(
                command.ManagedTenantId,
                command.FolderId,
                operation.PrincipalKind,
                operation.PrincipalId,
                operation.Action);
            LengthPrefixedHash.AppendField(hash, operation.Intent.ToString());
            LengthPrefixedHash.AppendField(hash, key.CanonicalValue);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static IReadOnlyList<string>? ValidateAndCanonicalizeTags(IReadOnlyList<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        if (tags.Count > MaxTagCount)
        {
            return null;
        }

        List<string> canonical = [];
        foreach (string tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag) || !FolderStreamName.IsValidSegment(tag))
            {
                return null;
            }

            canonical.Add(tag.Trim());
        }

        return canonical
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<FolderAccessOperation>? ValidateAndCanonicalizeAccessOperations(
        IFolderAccessCommand command,
        out FolderResultCode code)
    {
        code = FolderResultCode.Accepted;
        if (command.Operations.Count == 0)
        {
            code = FolderResultCode.ValidationFailed;
            return null;
        }

        FolderAccessOperationIntent requiredIntent = command switch
        {
            GrantFolderAccess => FolderAccessOperationIntent.Grant,
            RevokeFolderAccess => FolderAccessOperationIntent.Revoke,
            // Reaching this arm means a new IFolderAccessCommand implementer was added without
            // mapping it to a required intent — a contract bug, not a runtime input issue.
            _ => throw new InvalidOperationException(
                $"Unmapped IFolderAccessCommand implementation: {command.GetType().Name}."),
        };

        Dictionary<string, FolderAccessOperation> unique = new(StringComparer.Ordinal);
        Dictionary<string, FolderAccessOperationIntent> tupleIntents = new(StringComparer.Ordinal);
        foreach (FolderAccessOperation operation in command.Operations)
        {
            FolderResultCode? operationCode = ValidateOperation(operation, requiredIntent);
            if (operationCode is not null)
            {
                code = operationCode.Value;
                return null;
            }

            FolderAccessEntryKey key = new(
                command.ManagedTenantId,
                command.FolderId,
                operation.PrincipalKind,
                operation.PrincipalId,
                operation.Action);

            string tupleKey = key.CanonicalValue;

            // Defense-in-depth: ValidateOperation already rejects mixed intents within one
            // command via the `requiredIntent` check, so this branch is normally unreachable.
            // It survives so that a future loosening of ValidateOperation (allowing multiple
            // intents per command) cannot silently collapse same-tuple grant/revoke into a
            // duplicate map entry — the deterministic-conflict signal is preserved here.
            if (tupleIntents.TryGetValue(tupleKey, out FolderAccessOperationIntent priorIntent)
                && priorIntent != operation.Intent)
            {
                code = FolderResultCode.ConflictingEntry;
                return null;
            }

            tupleIntents[tupleKey] = operation.Intent;
            unique[$"{operation.Intent}|{tupleKey}"] = operation;
        }

        // Fingerprint depends on this post-dedup canonical ordering; FingerprintReflectsPostDedupOperations
        // locks that contract so a future change to dedup semantics cannot silently break idempotency
        // equivalence of fingerprints already recorded in the ledger.
        return unique
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => pair.Value)
            .ToArray();
    }

    internal static FolderResultCode? ValidateAccessOperation(FolderAccessOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!Enum.IsDefined(operation.PrincipalKind))
        {
            return FolderResultCode.InvalidPrincipal;
        }

        if (!Enum.IsDefined(operation.Intent))
        {
            return FolderResultCode.MalformedEvidence;
        }

        if (!FolderAccessAction.IsSupported(operation.Action))
        {
            return FolderResultCode.UnsupportedAction;
        }

        return IsValidPrincipalId(operation.PrincipalId)
            ? null
            : FolderResultCode.InvalidPrincipal;
    }

    private static FolderResultCode? ValidateOperation(
        FolderAccessOperation operation,
        FolderAccessOperationIntent requiredIntent)
    {
        FolderResultCode? code = ValidateAccessOperation(operation);
        if (code is not null)
        {
            return code;
        }

        return operation.Intent == requiredIntent ? null : FolderResultCode.ReplayConflict;
    }

    private static bool TryParseArchiveReasonCode(string? value, out FolderArchiveReasonCode reasonCode)
        => FolderArchiveReasonCodes.TryParse(value, out reasonCode);

    private static string ToContractValue(FolderArchiveReasonCode reasonCode)
        => FolderArchiveReasonCodes.ToContractValue(reasonCode);

    private static bool IsSafePathLabel(string? value)
        => string.IsNullOrWhiteSpace(value) || FolderStreamName.IsValidSegment(value);

    internal static bool IsValidPrincipalId(string? value) => IsValidIdentifier(value);

    internal static bool IsSafeEvidenceIdentifier(string? value)
    {
        if (!IsValidIdentifier(value))
        {
            return false;
        }

        string canonical = value!.ToLower(CultureInfo.InvariantCulture);

        // Substring scan catches payload-shaped leakage (path/URL/email markers).
        if (ForbiddenMetadataSubstrings.Any(term => canonical.Contains(term, StringComparison.Ordinal)))
        {
            return false;
        }

        // Word-boundary scan blocks identifier-shaped sensitive tokens without nulling
        // legitimate tenant IDs like `tenant-authority`, `acme-display-prod`, or
        // `tenant-branch-1` that merely contain those substrings.
        foreach (string segment in canonical.Split(IdentifierWordSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Array.IndexOf(ForbiddenMetadataWordTerms, segment) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsValidBranchRefToken(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 14 and <= 91
        && BranchRefTokenPattern().IsMatch(value);

    internal static bool AreValidBranchRefPatterns(IReadOnlyList<string>? values, bool required)
    {
        if (values is null)
        {
            return !required;
        }

        if ((required && values.Count == 0) || values.Count > MaxBranchRefPatternCount)
        {
            return false;
        }

        return values.All(static value => IsValidBranchRefToken(value));
    }

    internal static bool HasDuplicateBranchRefPatterns(IReadOnlyList<string>? values)
        => values is not null
        && values.Distinct(StringComparer.Ordinal).Count() != values.Count;

    internal static bool HasDuplicateBranchRefPatterns(IReadOnlyList<string> allowed, IReadOnlyList<string>? protectedPatterns)
        => protectedPatterns is not null
        && allowed.Intersect(protectedPatterns, StringComparer.Ordinal).Any();

    private static readonly char[] IdentifierWordSeparators = ['-', '_', '.'];

    private static bool IsSafeMetadata(string? value, bool required, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return !required;
        }

        // Normalize before the forbidden-term scan so confusables (NFD-decomposed combiners,
        // zero-width characters, Greek lookalikes) cannot bypass the blocklist while still
        // producing a normalized fingerprint downstream.
        string trimmed = value.Trim().Normalize(NormalizationForm.FormC);
        if (trimmed.Length > maxLength || trimmed.Any(c => char.IsControl(c) || IsInvisibleFormatChar(c)))
        {
            return false;
        }

        string canonical = trimmed.ToLower(CultureInfo.InvariantCulture);

        // Free-form metadata uses both blocklists as substring scans; tenant operators do
        // not put `branch`/`auth`/`display` into folder display names today and the
        // conservative stance here protects against PII leaking through DisplayName.
        if (ForbiddenMetadataSubstrings.Any(term => canonical.Contains(term, StringComparison.Ordinal)))
        {
            return false;
        }

        return !ForbiddenMetadataWordTerms.Any(term => canonical.Contains(term, StringComparison.Ordinal));
    }

    private static bool IsInvisibleFormatChar(char c)
        => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format
            || c == '​' // zero-width space
            || c == '‌' // zero-width non-joiner
            || c == '‍' // zero-width joiner
            || c == '﻿'; // BOM / zero-width no-break space

    [GeneratedRegex("\\A[a-z0-9._-]+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalIdentifierPattern();

    [GeneratedRegex("\\Abranch_ref_[a-z0-9_]{3,80}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex BranchRefTokenPattern();

    [GeneratedRegex("\\Aauthorref_[A-Za-z0-9_-]{6,118}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex AuthorMetadataReferencePattern();

    [GeneratedRegex("\\Abranchref_[A-Za-z0-9_-]{6,118}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex BranchRefTargetPattern();

    [GeneratedRegex("\\Adigest_[A-Za-z0-9_-]{9,121}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ChangedPathMetadataDigestPattern();

    [GeneratedRegex("\\A[a-z][a-z0-9_]{0,79}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex LowerSnakeMetadataPattern();

    [GeneratedRegex("\\Acommitref_[A-Za-z0-9_-]{6,118}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex CommitReferencePattern();

    [GeneratedRegex("\\Areconcile_[A-Za-z0-9_-]{6,118}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex CommitReconciliationReferencePattern();
}
