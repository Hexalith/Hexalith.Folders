using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;
using Hexalith.Folders.Contracts.Commands.Models;

namespace Hexalith.Folders.Contracts.Commands;

/// <summary>Archive a folder without exposing cross-tenant existence. Requires extensions.taskId of at most 128 lowercase letters, digits, '.', '_' or '-', not ending in '--'; the caller profile must allowlist taskId.</summary>
/// <param name="ArchiveReasonCode">The archiveReasonCode value.</param>
/// <param name="FolderId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="IdempotencyKey">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
[Description("Archive a folder without exposing cross-tenant existence. Requires extensions.taskId of at most 128 lowercase letters, digits, '.', '_' or '-', not ending in '--'; the caller profile must allowlist taskId.")]
[HexalithCommand("Archive a folder without exposing cross-tenant existence. Requires extensions.taskId of at most 128 lowercase letters, digits, '.', '_' or '-', not ending in '--'; the caller profile must allowlist taskId.", Domain = "folders",
    WireType = "Hexalith.Folders.Commands.ArchiveFolder",
    AggregateIdProperty = nameof(FolderId), IdempotencyKeyProperty = nameof(IdempotencyKey))]
public sealed record ArchiveFolder(
    [property: JsonPropertyName("archiveReasonCode"), Description("The archive reason code."), JsonRequired, Required]
    ArchiveFolderArchiveReasonCode ArchiveReasonCode,
    [property: HexalithIdentifier, JsonPropertyName("folderId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string FolderId,
    [property: HexalithIdentifier, JsonPropertyName("idempotencyKey"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string IdempotencyKey);
