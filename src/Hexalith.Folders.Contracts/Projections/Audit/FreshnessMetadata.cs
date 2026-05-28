using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public sealed record FreshnessMetadata(
    [property: JsonPropertyName("readConsistency")] string ReadConsistency,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt,
    [property: JsonPropertyName("projectionWatermark")] string? ProjectionWatermark,
    [property: JsonPropertyName("stale")] bool Stale,
    [property: JsonPropertyName("reasonCode")] string? ReasonCode);
