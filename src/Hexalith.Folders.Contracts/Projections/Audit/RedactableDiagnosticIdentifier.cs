using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public sealed record RedactableDiagnosticIdentifier(
    [property: JsonPropertyName("classification")] DiagnosticFieldClassification Classification,
    [property: JsonPropertyName("redaction")] RedactionMetadata Redaction,
    [property: JsonPropertyName("value")] string? Value);
