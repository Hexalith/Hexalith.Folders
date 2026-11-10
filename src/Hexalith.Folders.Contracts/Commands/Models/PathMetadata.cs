using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The PathMetadata request metadata.</summary>
/// <param name="NormalizedPath">Caller spelling preserved without retargeting. The file-policy v1.1.0 profile permits only ASCII A-Z a-z 0-9 . _ - / and rejects a leading/trailing slash, empty segment, complete . or .. segment, trailing dot in any segment, backslash, absolute/drive/UNC form, and Windows device base names in any ASCII case. The server restricts `.git` and descendants and rejects ordinal-ignore-case aliases for every component plus every touched symlink/reparse entry or ancestor without following it.</param>
/// <param name="DisplayName">Human-readable file name. Rejects control characters (0x00-0x1F, 0x7F), forward slashes, and backslashes to prevent log injection and path-separator confusion.</param>
/// <param name="PathPolicyClass">Closed, server-owned file-policy v1.1.0 classification. Callers cannot select or upgrade this value; any value carried through a request is revalidated against the pinned server policy. A required bounded include allowlist is evaluated first, exclusions always win with no re-inclusion, and invalid, unavailable, or changed policy fails closed.</param>
/// <param name="UnicodeNormalization">The unicodeNormalization value.</param>
[Description("The PathMetadata request metadata.")]
public sealed record PathMetadata(
    [property: JsonPropertyName("normalizedPath"), Description("Caller spelling preserved without retargeting. The file-policy v1.1.0 profile permits only ASCII A-Z a-z 0-9 . _ - / and rejects a leading/trailing slash, empty segment, complete . or .. segment, trailing dot in any segment, backslash, absolute/drive/UNC form, and Windows device base names in any ASCII case. The server restricts `.git` and descendants and rejects ordinal-ignore-case aliases for every component plus every touched symlink/reparse entry or ancestor without following it."), JsonRequired, Required, StringLength(500, MinimumLength = 1), RegularExpression("^(?!/)(?!.*(?:^|/)\\.{1,2}(?:$|/))(?!.*//)(?!.*[/]$)(?!.*(?:^|/)[^/]*\\.(?:/|$))(?!.*(?:^|/)(?:[Cc][Oo][Nn]|[Pp][Rr][Nn]|[Aa][Uu][Xx]|[Nn][Uu][Ll]|[Cc][Oo][Mm][1-9]|[Ll][Pp][Tt][1-9])(?:\\.[^/]*)?(?:$|/))[A-Za-z0-9._/-]+$")]
    string NormalizedPath,
    [property: JsonPropertyName("displayName"), Description("Human-readable file name. Rejects control characters (0x00-0x1F, 0x7F), forward slashes, and backslashes to prevent log injection and path-separator confusion."), JsonRequired, Required, StringLength(128, MinimumLength = 1), RegularExpression("^[^\\x00-\\x1F\\x7F/\\\\]+$")]
    string DisplayName,
    [property: JsonPropertyName("pathPolicyClass"), Description("Closed, server-owned file-policy v1.1.0 classification. Callers cannot select or upgrade this value; any value carried through a request is revalidated against the pinned server policy. A required bounded include allowlist is evaluated first, exclusions always win with no re-inclusion, and invalid, unavailable, or changed policy fails closed."), JsonRequired, Required]
    PathMetadataPathPolicyClass PathPolicyClass,
    [property: JsonPropertyName("unicodeNormalization"), Description("The unicode normalization."), JsonRequired, Required]
    PathMetadataUnicodeNormalization UnicodeNormalization);
