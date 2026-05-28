using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public sealed record ChangedPathEvidence(
    [property: JsonPropertyName("evidenceKind")] ChangedPathEvidenceKind EvidenceKind,
    [property: JsonPropertyName("classification")] DiagnosticFieldClassification Classification,
    [property: JsonPropertyName("digest")] string? Digest,
    [property: JsonPropertyName("reference")] string? Reference);
