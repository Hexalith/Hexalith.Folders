using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts.Projections.Audit;

namespace Hexalith.Folders.Queries.Audit;

public sealed record OperationTimelineEntryQueryResult(
    AuditQueryResultCode Code,
    OperationTimelineEntry? Entry,
    AuditFreshness Freshness,
    string? CorrelationId,
    string? TaskId,
    string OperationId,
    LayeredFolderAuthorizationResult? AuthorizationDenial);
