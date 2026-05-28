using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public sealed record RedactionMetadata(
    [property: JsonPropertyName("visibility")] RedactionVisibility Visibility,
    [property: JsonPropertyName("reasonCode")] string ReasonCode);
