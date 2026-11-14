using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.Folders.Projections.OperationalStatus;

namespace Hexalith.Folders.Workers;

/// <summary>Classifies a validated source rejection, including normalized unknown commands.</summary>
internal static class FoldersRejectedCommandClassifier
{
    /// <summary>Returns whether the actual source belongs to an organization or its independent provider binding.</summary>
    public static bool IsOrganization(EventStoreDomainEventEnvelope envelope)
        => FoldersRejectionSource.ReadOwner(envelope.Payload, envelope.TenantId, envelope.AggregateId,
            envelope.CorrelationId) != FoldersSourceOwnerKind.Folder;
}
