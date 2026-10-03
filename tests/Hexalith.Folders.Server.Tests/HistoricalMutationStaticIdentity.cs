using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authentication;

namespace Hexalith.Folders.Server.Tests;

internal sealed class HistoricalMutationStaticIdentity : ITenantContextAccessor, IEventStoreClaimTransformEvidenceAccessor
{
    public string? AuthoritativeTenantId => "tenant-a";

    public string? PrincipalId => "user-a";

    public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
        => EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", [actionToken]);
}
