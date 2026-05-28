using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.Audit;

public sealed record AuditRecordQuery(
    string FolderId,
    string AuditRecordId,
    string? AuthoritativeTenantId,
    string? PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string? CorrelationId,
    string? TaskId,
    IReadOnlyDictionary<string, string?>? ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?>? ClientControlledPrincipalValues);
