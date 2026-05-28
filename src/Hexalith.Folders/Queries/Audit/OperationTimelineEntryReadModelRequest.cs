namespace Hexalith.Folders.Queries.Audit;

public sealed record OperationTimelineEntryReadModelRequest(
    string ManagedTenantId,
    string FolderId,
    string TimelineEntryId,
    string PrincipalId,
    string ActionToken,
    string? TaskId,
    string? CorrelationId,
    string? AuthorizationWatermark,
    string ReadConsistency);
