using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts.Projections.Audit;

namespace Hexalith.Folders.Queries.Audit;

public sealed record AuditRecordQueryResult(
    AuditQueryResultCode Code,
    AuditRecord? Record,
    AuditFreshness Freshness,
    string? CorrelationId,
    string? TaskId,
    string OperationId,
    LayeredFolderAuthorizationResult? AuthorizationDenial);
