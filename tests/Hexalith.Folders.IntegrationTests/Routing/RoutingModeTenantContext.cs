using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authentication;

namespace Hexalith.Folders.IntegrationTests.Routing;

/// <summary>Supplies a fixed authenticated tenant, principal, and matching claim-transform evidence.</summary>
/// <param name="tenantId">The authoritative tenant.</param>
/// <param name="principalId">The authenticated principal.</param>
internal sealed class RoutingModeTenantContext(string tenantId, string principalId)
    : ITenantContextAccessor, IEventStoreClaimTransformEvidenceAccessor
{
    /// <inheritdoc/>
    public string? AuthoritativeTenantId { get; } = tenantId;

    /// <inheritdoc/>
    public string? PrincipalId { get; } = principalId;

    /// <inheritdoc/>
    public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
        => EventStoreClaimTransformEvidence.Allowed(
            AuthoritativeTenantId ?? string.Empty,
            PrincipalId ?? string.Empty,
            [actionToken]);
}
