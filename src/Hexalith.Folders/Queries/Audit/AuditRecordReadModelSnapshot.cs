using Hexalith.Folders.Contracts.Projections.Audit;

namespace Hexalith.Folders.Queries.Audit;

public sealed record AuditRecordReadModelSnapshot(
    string ManagedTenantId,
    string FolderId,
    AuditRecord Record,
    AuditFreshness Freshness);
