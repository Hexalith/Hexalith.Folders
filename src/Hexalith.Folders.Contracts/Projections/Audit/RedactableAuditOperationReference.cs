using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public sealed record RedactableAuditOperationReference(
    [property: JsonPropertyName("classification")] DiagnosticFieldClassification Classification,
    [property: JsonPropertyName("redaction")] RedactionMetadata Redaction,
    [property: JsonPropertyName("value")] string? Value);
