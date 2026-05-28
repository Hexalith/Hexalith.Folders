namespace Hexalith.Folders.Queries.Audit;

public sealed record AuditRecordReadModelRequest(
    string ManagedTenantId,
    string FolderId,
    string AuditRecordId,
    string PrincipalId,
    string ActionToken,
    string? TaskId,
    string? CorrelationId,
    string? AuthorizationWatermark,
    string ReadConsistency);
