using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.Audit;

public sealed record AuditTrailQuery(
    string FolderId,
    string? AuthoritativeTenantId,
    string? PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string? CorrelationId,
    string? TaskId,
    string? Cursor,
    int? RequestedLimit,
    string? Filter,
    IReadOnlyDictionary<string, string?>? ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?>? ClientControlledPrincipalValues);
