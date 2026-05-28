using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public sealed record RedactableAuditTimestamp(
    [property: JsonPropertyName("precision")] RedactableAuditTimestampPrecision Precision,
    [property: JsonPropertyName("redaction")] RedactionMetadata Redaction,
    [property: JsonPropertyName("value")] DateTimeOffset? Value);
