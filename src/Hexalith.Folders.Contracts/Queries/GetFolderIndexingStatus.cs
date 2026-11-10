using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;

namespace Hexalith.Folders.Contracts.Queries;

/// <summary>Inspect the metadata-only semantic-indexing status of a folder's file versions.</summary>
/// <param name="FolderId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="TaskId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
[Description("Inspect the metadata-only semantic-indexing status of a folder's file versions.")]
[HexalithQuery("Inspect the metadata-only semantic-indexing status of a folder's file versions.", Domain = "folders",
    WireType = "Hexalith.Folders.Queries.GetFolderIndexingStatus",
    ProjectionType = "get-folder-indexing-status", AggregateIdProperty = nameof(FolderId))]
public sealed record GetFolderIndexingStatus(
    [property: HexalithIdentifier, JsonPropertyName("folderId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string FolderId,
    [property: HexalithIdentifier, JsonPropertyName("taskId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string? TaskId = null);
