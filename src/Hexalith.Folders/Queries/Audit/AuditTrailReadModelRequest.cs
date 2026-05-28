namespace Hexalith.Folders.Queries.Audit;

public sealed record AuditTrailReadModelRequest(
    string ManagedTenantId,
    string FolderId,
    string PrincipalId,
    string ActionToken,
    string? Cursor,
    int Limit,
    string? TaskId,
    string? CorrelationId,
    string? AuthorizationWatermark,
    string ReadConsistency);
