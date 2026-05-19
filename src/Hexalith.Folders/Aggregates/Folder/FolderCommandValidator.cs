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
    private const int MaxDisplayNameLength = 128;
    private const int MaxDescriptionLength = 512;

    private static readonly string[] ForbiddenMetadataTerms =
    [
        "credential",
        "token",
        "secret",
        "repository",
        "repo-",
        "repo_",
        "branch",
        "raw file",
        "file content",
        "diff --git",
        "generated context",
        "provider payload",
        "unauthorized",
        "email",
        "auth",
        "display",
        "@",
        "://",
        "\\",
        "/",
        "|",
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

        if (!IsValidIdentifier(command.ActorPrincipalId)
            || !IsValidIdentifier(command.CorrelationId)
            || !IsValidIdentifier(command.TaskId)
            || !IsValidIdentifier(command.IdempotencyKey))
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
            _ => (FolderAccessOperationIntent)(-1),
        };

        if (!Enum.IsDefined(requiredIntent))
        {
            code = FolderResultCode.ValidationFailed;
            return null;
        }

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
            if (tupleIntents.TryGetValue(tupleKey, out FolderAccessOperationIntent priorIntent)
                && priorIntent != operation.Intent)
            {
                code = FolderResultCode.ReplayConflict;
                return null;
            }

            tupleIntents[tupleKey] = operation.Intent;
            unique[$"{operation.Intent}|{tupleKey}"] = operation;
        }

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
        return !ForbiddenMetadataTerms.Any(term => canonical.Contains(term, StringComparison.Ordinal));
    }

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
        return !ForbiddenMetadataTerms.Any(term => canonical.Contains(term, StringComparison.Ordinal));
    }

    private static bool IsInvisibleFormatChar(char c)
        => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format
            || c == '​' // zero-width space
            || c == '‌' // zero-width non-joiner
            || c == '‍' // zero-width joiner
            || c == '﻿'; // BOM / zero-width no-break space

    [GeneratedRegex("^[a-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalIdentifierPattern();
}
