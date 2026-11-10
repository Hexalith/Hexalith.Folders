using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;
using Hexalith.Folders.Contracts.Queries.Models;

namespace Hexalith.Folders.Contracts.Queries;

/// <summary>Read authorized bounded file-range metadata.</summary>
/// <param name="Path">File-policy v1.1.0 workspace-root-relative path metadata. Paths preserve the caller&#x27;s accepted spelling, use forward slashes, have no leading or trailing slash, are declared NFC, and are never local or absolute filesystem paths. The server does not lowercase, case-fold, separator-convert, Unicode-rewrite, alias-resolve, follow links, or otherwise retarget a path. Traversal, reserved names, a trailing space or dot in any segment, `.git` and its descendants, ordinal-ignore-case component aliases, and every touched symlink/reparse entry or ancestor are rejected before observation.</param>
/// <param name="StartOffset">Inclusive zero-based byte offset. May reference any non-negative offset within the authorized file.</param>
/// <param name="EndOffset">Exclusive byte offset. Derived rule: `endOffset &gt;= startOffset` and `endOffset - startOffset &lt;= 262144` (single read window). Larger files require multiple bounded calls. TODO(reference-pending): docs/exit-criteria/c4-input-limits.md PM approval state is proposed.</param>
/// <param name="FolderId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="WorkspaceId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="TaskId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
[Description("Read authorized bounded file-range metadata.")]
[HexalithQuery("Read authorized bounded file-range metadata.", Domain = "folders",
    WireType = "Hexalith.Folders.Queries.ReadFileRange",
    ProjectionType = "read-file-range", AggregateIdProperty = nameof(FolderId))]
public sealed record ReadFileRange(
    [property: JsonPropertyName("path"), Description("File-policy v1.1.0 workspace-root-relative path metadata. Paths preserve the caller's accepted spelling, use forward slashes, have no leading or trailing slash, are declared NFC, and are never local or absolute filesystem paths. The server does not lowercase, case-fold, separator-convert, Unicode-rewrite, alias-resolve, follow links, or otherwise retarget a path. Traversal, reserved names, a trailing space or dot in any segment, `.git` and its descendants, ordinal-ignore-case component aliases, and every touched symlink/reparse entry or ancestor are rejected before observation.\n"), JsonRequired, Required]
    PathMetadata Path,
    [property: JsonPropertyName("startOffset"), Description("Inclusive zero-based byte offset. May reference any non-negative offset within the authorized file."), JsonRequired, Required, Range(typeof(long), "0", "9223372036854775807")]
    long StartOffset,
    [property: JsonPropertyName("endOffset"), Description("Exclusive byte offset. Derived rule: `endOffset >= startOffset` and `endOffset - startOffset <= 262144` (single read window). Larger files require multiple bounded calls. TODO(reference-pending): docs/exit-criteria/c4-input-limits.md PM approval state is proposed."), JsonRequired, Required, Range(typeof(long), "0", "9223372036854775807")]
    long EndOffset,
    [property: HexalithIdentifier, JsonPropertyName("folderId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string FolderId,
    [property: HexalithIdentifier, JsonPropertyName("workspaceId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string WorkspaceId,
    [property: HexalithIdentifier, JsonPropertyName("taskId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string? TaskId = null);
