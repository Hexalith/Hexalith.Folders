using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public sealed record OperationTimelinePage(
    [property: JsonPropertyName("entries")] IReadOnlyList<OperationTimelineEntry> Entries,
    [property: JsonPropertyName("page")] PaginationMetadata Page,
    [property: JsonPropertyName("retentionClass")] string RetentionClass,
    [property: JsonPropertyName("freshness")] FreshnessMetadata Freshness);
