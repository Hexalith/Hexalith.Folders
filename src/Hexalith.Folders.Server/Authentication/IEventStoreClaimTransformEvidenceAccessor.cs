using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Authentication;

public interface IEventStoreClaimTransformEvidenceAccessor
{
    EventStoreClaimTransformEvidence GetEvidence(string actionToken);
}
