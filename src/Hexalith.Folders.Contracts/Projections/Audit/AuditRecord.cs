using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public sealed record AuditRecord(
    [property: JsonPropertyName("auditRecordId")] string AuditRecordId,
    [property: JsonPropertyName("actorReference")] RedactableAuditActorReference ActorReference,
    [property: JsonPropertyName("operationId")] RedactableAuditOperationReference OperationId,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("resultStatus")] string ResultStatus,
    [property: JsonPropertyName("sanitizedErrorCategory")] string SanitizedErrorCategory,
    [property: JsonPropertyName("retryable")] bool Retryable,
    [property: JsonPropertyName("durationMilliseconds")] long DurationMilliseconds,
    [property: JsonPropertyName("evidenceTimestamp")] RedactableAuditTimestamp EvidenceTimestamp,
    [property: JsonPropertyName("redaction")] RedactionMetadata Redaction,
    [property: JsonPropertyName("freshness")] FreshnessMetadata Freshness,
    [property: JsonPropertyName("taskId")] string? TaskId,
    [property: JsonPropertyName("changedPathEvidence")] ChangedPathEvidence? ChangedPathEvidence);
