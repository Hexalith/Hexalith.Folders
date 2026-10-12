using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authentication;

namespace Hexalith.Folders.Server.Tests;

internal sealed class ProductionProcessClaimTransformEvidenceAccessor : IEventStoreClaimTransformEvidenceAccessor
{
    public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
        => EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", [actionToken]);
}
