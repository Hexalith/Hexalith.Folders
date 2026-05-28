using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Projections.Audit;

public sealed record OperationTimelineEntry(
    [property: JsonPropertyName("timelineEntryId")] string TimelineEntryId,
    [property: JsonPropertyName("operationId")] string OperationId,
    [property: JsonPropertyName("taskId")] string TaskId,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("workspaceReference")] RedactableDiagnosticIdentifier WorkspaceReference,
    [property: JsonPropertyName("stateTransition")] DiagnosticStateTransition StateTransition,
    [property: JsonPropertyName("sanitizedResult")] string SanitizedResult,
    [property: JsonPropertyName("retryable")] bool Retryable,
    [property: JsonPropertyName("durationMilliseconds")] long DurationMilliseconds,
    [property: JsonPropertyName("evidenceTimestamp")] DateTimeOffset EvidenceTimestamp,
    [property: JsonPropertyName("freshness")] FreshnessMetadata Freshness);
