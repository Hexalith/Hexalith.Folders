using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Content;

/// <summary>Uses admitted actor state during commands and authenticated retained streams during reads/recovery.</summary>
internal sealed class WorkspaceContentStateReader(IFolderRepository repository, IEventStoreGatewayClient gateway,
    FolderEventReplay replay, TimeProvider clock)
{
    internal async Task<WorkspaceContentState> ReadAsync(ContentScope scope, CancellationToken token)
    {
        if (repository is ActorFoldedFolderRepository actor && actor.TryRead(scope.TenantId, scope.AggregateId, out var state, out var raw))
        {
            DomainServiceCurrentState? source = DomainStateReplay.SnapshotAware(raw);
            return new(state!, source?.CurrentSequence ?? 0, (source?.Events ?? []).Select(item => new StreamReadEvent(
                item.Metadata.SequenceNumber, item.Metadata.EventTypeName, item.Payload, item.Metadata.SerializationFormat,
                item.Metadata.MetadataVersion, item.Metadata.MessageId, item.Metadata.CorrelationId, item.Metadata.CausationId,
                item.Metadata.Timestamp, item.Metadata.UserId) { PayloadVersion = item.Metadata.PayloadVersion }).ToArray(), true);
        }
        var result = await new AuthoritativeEventStreamReader(gateway, clock, TimeSpan.FromSeconds(2))
            .ReadAsync(new AggregateIdentity(scope.TenantId, scope.Domain, scope.AggregateId), token).ConfigureAwait(false);
        var stream = result.Stream ?? throw new ContentUnavailableException();
        EventEnvelope[] events = stream.Events.Select(item => new EventEnvelope(new EventMetadata(item.MessageId,
            scope.AggregateId, "folder", scope.TenantId, scope.Domain, item.SequenceNumber, item.GlobalPosition, item.Timestamp,
            item.CorrelationId ?? "source", item.CausationId ?? "source", item.UserId ?? "source", "source", item.EventTypeName,
            item.MetadataVersion, item.SerializationFormat) { PayloadVersion = item.PayloadVersion }, item.Payload, null)).ToArray();
        var folded = replay.FoldFolder(new DomainServiceCurrentState(null, events, 0, stream.Head), scope.TenantId, scope.AggregateId);
        return new(folded, stream.Head, stream.Events, false);
    }
}
