using Hexalith.Folders.Contracts.Projections.Audit;

namespace Hexalith.Folders.Queries.Audit;

public sealed record OperationTimelineEntryReadModelSnapshot(
    string ManagedTenantId,
    string FolderId,
    OperationTimelineEntry Entry,
    AuditFreshness Freshness);
