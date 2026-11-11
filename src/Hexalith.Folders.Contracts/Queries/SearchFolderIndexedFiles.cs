using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;
using Hexalith.Folders.Contracts.Queries.Models;

namespace Hexalith.Folders.Contracts.Queries;

/// <summary>Search the authorized Folders semantic search index without content.</summary>
/// <param name="QueryFamily">The queryFamily value.</param>
/// <param name="QueryText">Search input is request payload only. Audit metadata records query family and counts, never raw search text.</param>
/// <param name="FolderId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="WorkspaceId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="Limit">Maximum number of indexed file results, from 1 through 500; defaults to 500 when omitted.</param>
/// <param name="Cursor">Opaque, non-secret pagination cursor returned by a prior call&#x27;s `nextCursor`. Echo back to retrieve the next page. The server MUST treat the cursor as non-authoritative for tenant access, ACL, or sensitivity classification.</param>
/// <param name="TaskId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
[Description("Search the authorized Folders semantic search index without content.")]
[HexalithQuery("Search the authorized Folders semantic search index without content.", Domain = "folders",
    WireType = "Hexalith.Folders.Queries.SearchFolderIndexedFiles",
    ProjectionType = "search-folder-indexed-files", AggregateIdProperty = nameof(FolderId))]
public sealed record SearchFolderIndexedFiles(
    [property: JsonPropertyName("queryFamily"), Description("The query family."), JsonRequired, Required]
    SearchFolderIndexedFilesQueryFamily QueryFamily,
    [property: JsonPropertyName("queryText"), Description("Search input is request payload only. Audit metadata records query family and counts, never raw search text."), JsonRequired, Required, StringLength(256, MinimumLength = 1)]
    string QueryText,
    [property: HexalithIdentifier, JsonPropertyName("folderId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string FolderId,
    [property: HexalithIdentifier, JsonPropertyName("workspaceId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string WorkspaceId,
    [property: JsonPropertyName("limit"), Description("Maximum number of indexed file results, from 1 through 500; defaults to 500 when omitted."), Range(1, 500)]
    int? Limit = null,
    [property: JsonPropertyName("cursor"), Description("Opaque, non-secret pagination cursor returned by a prior call's `nextCursor`. Echo back to retrieve the next page. The server MUST treat the cursor as non-authoritative for tenant access, ACL, or sensitivity classification."), StringLength(256)]
    string? Cursor = null,
    [property: HexalithIdentifier, JsonPropertyName("taskId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string? TaskId = null);
