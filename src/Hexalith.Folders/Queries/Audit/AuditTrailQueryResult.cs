using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts.Projections.Audit;

namespace Hexalith.Folders.Queries.Audit;

public sealed record AuditTrailQueryResult(
    AuditQueryResultCode Code,
    AuditTrailPage? Page,
    AuditFreshness Freshness,
    string? CorrelationId,
    string? TaskId,
    string OperationId,
    LayeredFolderAuthorizationResult? AuthorizationDenial);
