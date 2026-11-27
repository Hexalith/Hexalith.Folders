using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed record ProviderSupportEvidenceQuery(
    string? AuthoritativeTenantId,
    string? PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string? CorrelationId,
    string? Cursor,
    int Limit,
    IReadOnlyDictionary<string, string?>? ClientControlledTenantValues)
{
    /// <summary>Client-supplied principal identifiers used for comparison with the authenticated caller.</summary>
    internal IReadOnlyDictionary<string, string?>? ClientControlledPrincipalValues { get; init; }
}
