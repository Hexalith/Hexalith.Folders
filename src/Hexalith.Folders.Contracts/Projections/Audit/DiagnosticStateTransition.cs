using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public sealed record DiagnosticStateTransition(
    [property: JsonPropertyName("fromState")] string FromState,
    [property: JsonPropertyName("toState")] string ToState,
    [property: JsonPropertyName("disposition")] string Disposition);
