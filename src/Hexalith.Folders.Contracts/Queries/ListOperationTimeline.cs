using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;

namespace Hexalith.Folders.Contracts.Queries;

/// <summary>List authorized operation timeline metadata.</summary>
/// <param name="FolderId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="Cursor">The cursor value.</param>
/// <param name="Limit">Maximum number of operation timeline entries to return, from 1 through 1000.</param>
/// <param name="Filter">The filter value.</param>
[Description("List authorized operation timeline metadata.")]
[HexalithQuery("List authorized operation timeline metadata.", Domain = "folders",
    WireType = "Hexalith.Folders.Queries.ListOperationTimeline",
    ProjectionType = "list-operation-timeline", AggregateIdProperty = nameof(FolderId))]
public sealed record ListOperationTimeline(
    [property: HexalithIdentifier, JsonPropertyName("folderId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string FolderId,
    [property: JsonPropertyName("cursor"), Description("The cursor."), StringLength(256, MinimumLength = 1)]
    string? Cursor = null,
    [property: JsonPropertyName("limit"), Description("Maximum number of operation timeline entries to return, from 1 through 1000."), Range(1, 1000)]
    int? Limit = null,
    [property: JsonPropertyName("filter"), Description("The filter."), StringLength(256, MinimumLength = 1), RegularExpression("^[a-z][A-Za-z0-9_=.,*\\- ]{0,255}$")]
    string? Filter = null);
