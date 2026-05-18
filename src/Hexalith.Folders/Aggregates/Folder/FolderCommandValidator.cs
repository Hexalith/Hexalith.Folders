using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Hexalith.Folders.Aggregates.Folder;

public static partial class FolderCommandValidator
{
    internal const int MaxIdentifierLength = 256;
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

    private static string Fingerprint(CreateFolder command, IReadOnlyList<string> tags)
    {
        string[] parts =
        [
            command.CommandType,
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            CanonicalMetadata(command.DisplayName),
            CanonicalMetadata(command.Description),
            CanonicalMetadata(command.PathLabel),
            string.Join(",", tags),
            command.ActorPrincipalId,
        ];

        return string.Join("|", parts);
    }

    private static IReadOnlyList<string>? ValidateAndCanonicalizeTags(IReadOnlyList<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        List<string> canonical = [];
        foreach (string tag in tags)
        {
            if (!IsSafePathLabel(tag))
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

    private static bool IsSafePathLabel(string? value)
        => string.IsNullOrWhiteSpace(value) || FolderStreamName.IsValidSegment(value);

    private static bool IsSafeMetadata(string? value, bool required, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return !required;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > maxLength || trimmed.Any(char.IsControl))
        {
            return false;
        }

        string canonical = trimmed.ToLower(CultureInfo.InvariantCulture);
        return !ForbiddenMetadataTerms.Any(term => canonical.Contains(term, StringComparison.Ordinal));
    }

    [GeneratedRegex("^[a-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalIdentifierPattern();
}
