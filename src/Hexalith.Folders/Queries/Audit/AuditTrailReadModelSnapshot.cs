using Hexalith.Folders.Contracts.Projections.Audit;

namespace Hexalith.Folders.Queries.Audit;

public sealed record AuditTrailReadModelSnapshot(
    string ManagedTenantId,
    string FolderId,
    IReadOnlyList<AuditRecord> Entries,
    string? NextCursor,
    bool IsTruncated,
    string? TruncatedReason,
    AuditFreshness Freshness);
