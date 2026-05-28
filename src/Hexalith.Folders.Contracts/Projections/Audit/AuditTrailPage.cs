using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public sealed record AuditTrailPage(
    [property: JsonPropertyName("entries")] IReadOnlyList<AuditRecord> Entries,
    [property: JsonPropertyName("page")] PaginationMetadata Page,
    [property: JsonPropertyName("retentionClass")] string RetentionClass,
    [property: JsonPropertyName("freshness")] FreshnessMetadata Freshness);
