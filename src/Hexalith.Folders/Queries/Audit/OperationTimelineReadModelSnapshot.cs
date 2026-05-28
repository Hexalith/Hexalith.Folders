using Hexalith.Folders.Contracts.Projections.Audit;

namespace Hexalith.Folders.Queries.Audit;

public sealed record OperationTimelineReadModelSnapshot(
    string ManagedTenantId,
    string FolderId,
    IReadOnlyList<OperationTimelineEntry> Entries,
    string? NextCursor,
    bool IsTruncated,
    string? TruncatedReason,
    AuditFreshness Freshness);
