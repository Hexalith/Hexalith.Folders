using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public sealed record PaginationMetadata(
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("isTruncated")] bool IsTruncated,
    [property: JsonPropertyName("cursor")] string? Cursor,
    [property: JsonPropertyName("requestedLimit")] int? RequestedLimit,
    [property: JsonPropertyName("truncatedReason")] string? TruncatedReason);
