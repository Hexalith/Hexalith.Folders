using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed record ProviderSupportEvidenceQuery(
    string? AuthoritativeTenantId,
    string? PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string? CorrelationId,
    string? Cursor,
    int Limit,
    IReadOnlyDictionary<string, string?>? ClientControlledTenantValues);
