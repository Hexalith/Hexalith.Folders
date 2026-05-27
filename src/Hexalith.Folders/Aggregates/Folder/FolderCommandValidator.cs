using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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
            && CanonicalIdentifierPattern().IsMatch(value);

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

        AppendField(hash, command.CommandType);
        AppendField(hash, command.ManagedTenantId);
        AppendField(hash, command.OrganizationId);
        AppendField(hash, command.FolderId);
        AppendField(hash, CanonicalMetadata(command.DisplayName));
        AppendField(hash, CanonicalMetadata(command.Description));
        AppendField(hash, CanonicalMetadata(command.PathLabel));
        AppendInt32(hash, tags.Count);
        foreach (string tag in tags)
        {
            AppendField(hash, tag);
        }

        AppendField(hash, command.ActorPrincipalId);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Fingerprint(ArchiveFolder command, FolderArchiveReasonCode archiveReasonCode)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendField(hash, command.CommandType);
        AppendField(hash, command.ManagedTenantId);
        AppendField(hash, command.OrganizationId);
        AppendField(hash, command.FolderId);
        AppendField(hash, command.RequestSchemaVersion);
        AppendField(hash, ToContractValue(archiveReasonCode));
        AppendField(hash, command.ActorPrincipalId);
        AppendField(hash, command.CorrelationId);
        AppendField(hash, command.TaskId);
        AppendField(hash, command.IdempotencyKey);
        AppendField(hash, command.PayloadTenantId);
        AppendInt32(hash, command.ClientControlledTenantIds.Count);
        foreach (KeyValuePair<string, string?> tenantEntry in command.ClientControlledTenantIds.OrderBy(static x => x.Key, StringComparer.Ordinal))
        {
            AppendField(hash, tenantEntry.Key);
            AppendField(hash, tenantEntry.Value);
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
        AppendField(hash, "archive-decision-v1");
        AppendField(hash, commandFingerprint);
        AppendField(hash, command.IdempotencyKey);
        AppendField(hash, policyVersion);
        AppendField(hash, freshnessWatermark);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Fingerprint(CreateRepositoryBackedFolder command)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendField(hash, command.CommandType);
        AppendField(hash, command.ManagedTenantId);
        AppendField(hash, command.OrganizationId);
        AppendField(hash, command.FolderId);
        AppendField(hash, command.RequestSchemaVersion);
        AppendField(hash, command.CredentialScopeClass);
        AppendField(hash, CanonicalMetadata(command.FolderMetadataDisplayName));
        AppendField(hash, command.ProviderBindingRef);
        AppendField(hash, command.RepositoryBindingId);
        AppendField(hash, command.RepositoryProfileRef);
        AppendField(hash, command.BranchRefPolicyRef);

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
        AppendField(hash, "repository-binding-id-v1");
        AppendField(hash, managedTenantId);
        AppendField(hash, folderId);
        AppendField(hash, providerBindingRef);
        AppendField(hash, externalRepositoryRef);
        AppendField(hash, branchRefPolicyRef);

        string digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        return $"repository_binding_{digest[..32]}";
    }

    internal static string ExternalRepositoryRefFingerprint(BindRepository command)
    {
        ArgumentNullException.ThrowIfNull(command);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, "external-repository-ref-v1");
        AppendField(hash, command.ManagedTenantId);
        AppendField(hash, command.ProviderBindingRef);
        AppendField(hash, command.ExternalRepositoryRef);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Fingerprint(BindRepository command)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendField(hash, command.CommandType);
        AppendField(hash, command.ManagedTenantId);
        AppendField(hash, command.OrganizationId);
        AppendField(hash, command.FolderId);
        AppendField(hash, command.RequestSchemaVersion);
        AppendField(hash, command.BranchRefPolicyRef);
        AppendField(hash, command.CredentialScopeClass);
        AppendField(hash, command.ExternalRepositoryRef);
        AppendField(hash, command.ProviderBindingRef);
        AppendField(hash, command.RepositoryBindingId);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Fingerprint(ConfigureBranchRefPolicy command)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendInt32(hash, command.AllowedRefPatterns.Count);
        foreach (string pattern in command.AllowedRefPatterns)
        {
            AppendField(hash, pattern);
        }

        AppendField(hash, command.DefaultRef);
        AppendField(hash, command.PolicyRef);
        AppendInt32(hash, command.ProtectedRefPatterns?.Count ?? -1);
        if (command.ProtectedRefPatterns is not null)
        {
            foreach (string pattern in command.ProtectedRefPatterns)
            {
                AppendField(hash, pattern);
            }
        }

        AppendField(hash, command.FolderId);
        AppendField(hash, command.RepositoryBindingId);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Fingerprint(PrepareWorkspace command)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendField(hash, command.BranchRefPolicyRef);
        AppendField(hash, command.FolderId);
        AppendField(hash, command.RepositoryBindingId);
        AppendField(hash, command.TaskId);
        AppendField(hash, command.WorkspaceId);
        AppendField(hash, command.WorkspacePolicyRef);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Fingerprint(LockWorkspace command)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendField(hash, command.FolderId);
        AppendField(hash, command.LockIntent);
        AppendInt32(hash, command.RequestedLeaseSeconds);
        AppendField(hash, command.TaskId);
        AppendField(hash, command.WorkspaceId);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    internal static string DeriveWorkspaceLockId(LockWorkspace command, string idempotencyFingerprint)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyFingerprint);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, "workspace-lock-id-v1");
        AppendField(hash, command.ManagedTenantId);
        AppendField(hash, command.FolderId);
        AppendField(hash, command.WorkspaceId);
        AppendField(hash, command.TaskId);
        AppendField(hash, idempotencyFingerprint);

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
        AppendField(hash, "workspace-lock-ownership-proof-v1");
        AppendField(hash, managedTenantId);
        AppendField(hash, folderId);
        AppendField(hash, workspaceId);
        AppendField(hash, taskId);
        AppendField(hash, lockId);

        string digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        return $"lock_proof_{digest[..32]}";
    }

    private static string Fingerprint(ReleaseWorkspaceLock command)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendField(hash, command.FolderId);
        AppendField(hash, command.LockId);
        AppendField(hash, command.LockOwnershipProof);
        AppendField(hash, command.TaskId);
        AppendField(hash, command.WorkspaceId);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

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

        AppendField(hash, command.CommandType);
        AppendField(hash, command.ManagedTenantId);
        AppendField(hash, command.OrganizationId);
        AppendField(hash, command.FolderId);
        AppendField(hash, command.ActorPrincipalId);
        AppendInt32(hash, operations.Count);
        foreach (FolderAccessOperation operation in operations)
        {
            FolderAccessEntryKey key = new(
                command.ManagedTenantId,
                command.FolderId,
                operation.PrincipalKind,
                operation.PrincipalId,
                operation.Action);
            AppendField(hash, operation.Intent.ToString());
            AppendField(hash, key.CanonicalValue);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendField(IncrementalHash hash, string? value)
    {
        ReadOnlySpan<byte> bytes = value is null
            ? ReadOnlySpan<byte>.Empty
            : Encoding.UTF8.GetBytes(value);
        AppendInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        hash.AppendData(buffer);
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

    [GeneratedRegex("^[a-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalIdentifierPattern();

    [GeneratedRegex("^branch_ref_[a-z0-9_]{3,80}$", RegexOptions.CultureInvariant)]
    private static partial Regex BranchRefTokenPattern();
}
