using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Observability;

public sealed record FolderAuditObservation(
    FolderAuditOperationKind OperationKind,
    FolderAuditResult Result,
    string TenantId,
    string? ActorReference,
    string? TaskId,
    string? OperationId,
    string? CorrelationId,
    string? FolderId,
    string? WorkspaceId,
    string? ProviderReference,
    DateTimeOffset Timestamp,
    TimeSpan Duration,
    FolderAuditRedactionState RedactionState,
    string? StateTransition,
    string? SanitizedCategory,
    bool IsRetry,
    bool IsIdempotentReplay,
    bool IsDuplicate,
    IReadOnlyDictionary<string, string> Classifications)
{
    private static readonly JsonSerializerOptions ToStringJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string DurationEvidence => RedactionState == FolderAuditRedactionState.Redacted
        ? FolderAuditSanitizer.DurationBucket(Duration)
        : Duration.TotalMilliseconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    public bool IsFailure => Result is FolderAuditResult.Denied
        or FolderAuditResult.Failed
        or FolderAuditResult.Rejected
        or FolderAuditResult.Stale
        or FolderAuditResult.Unavailable;

    public override string ToString()
        => JsonSerializer.Serialize(new
        {
            operationKind = OperationKind.ToString(),
            result = Result.ToString(),
            correlationId = CorrelationId,
            taskId = TaskId,
            category = SanitizedCategory,
            redactionState = RedactionState.ToString(),
            duration = DurationEvidence,
            retry = IsRetry,
            replay = IsIdempotentReplay,
            duplicate = IsDuplicate,
        }, ToStringJsonOptions);
}
