using System.Globalization;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Retains only compact current state, one requested historical view/earliest closure, or one message disposition.</summary>
/// <param name="tenantId">The already authorized source tenant.</param>
/// <param name="folderId">The exact authorized aggregate.</param>
/// <param name="viewKey">The one historical view selected, if any.</param>
/// <param name="candidate">The exact original candidate whose identity is selected, if any.</param>
/// <param name="checkpointSequence">The selected predecessor source boundary, if any.</param>
/// <param name="viewCutSequence">The exact selected derived checkpoint cut; the full native prefix still must verify.</param>
internal sealed class OperationalStatusNativeSourceFold(string tenantId, string folderId, string? viewKey = null,
    OperationalStatusEvent? candidate = null, long? checkpointSequence = null, long? viewCutSequence = null) : INativeSourceScanFold<OperationalStatusNativeSourceFacts>
{
    private OperationalStatusCheckpoint? _checkpoint;
    private OperationalStatusTaskCompletion? _completion;
    private string? _valueDigest;
    private string? _viewPrefix;
    private long? _viewSequence;
    private long _sequence;
    private bool _candidateFound;
    private bool _messageReused;
    private bool _candidateConflict;
    private string _sourceDigest = OperationalStatusKeys.Digest(tenantId, folderId);
    private string? _checkpointSourceDigest;
    private bool _checkpointNeedsIntegrity;

    /// <inheritdoc/>
    public bool TryApply(StreamReadEvent sourceEvent)
    {
        var source = new ProjectionEventDto(sourceEvent.EventTypeName, sourceEvent.Payload, sourceEvent.SerializationFormat,
            sourceEvent.SequenceNumber, sourceEvent.Timestamp, sourceEvent.CorrelationId!, sourceEvent.MessageId,
            sourceEvent.UserId, sourceEvent.GlobalPosition)
        { MetadataVersion = sourceEvent.MetadataVersion, StoredPayloadVersion = sourceEvent.PayloadVersion };
        OperationalStatusEvent item = OperationalStatusEventReader.ReadStored(source);
        if (source.EventTypeName == "Hexalith.Folders.Server.FolderCommandRejected"
            && FoldersRejectionSource.ReadOwner(source.Payload, tenantId, folderId, source.CorrelationId) != FoldersSourceOwnerKind.Folder)
            return false;
        if (item.Sequence != _sequence + 1) return false;
        _sequence = item.Sequence;
        _sourceDigest = OperationalStatusKeys.Digest(_sourceDigest, item.Digest);
        if (checkpointSequence == _sequence) _checkpointSourceDigest = _sourceDigest;
        if (candidate is not null)
        {
            if (item.Sequence == candidate.Sequence)
            {
                _candidateFound = item.MessageId == candidate.MessageId && item.Digest == candidate.Digest && candidate.Source is { } supplied
                    && supplied.GlobalPosition == source.GlobalPosition && supplied.UserId == source.UserId
                    && supplied.Timestamp.EqualsExact(source.Timestamp)
                    && supplied.EventTypeName == source.EventTypeName && supplied.CorrelationId == source.CorrelationId
                    && supplied.SerializationFormat == source.SerializationFormat
                    && supplied.MetadataVersion == source.MetadataVersion && supplied.StoredPayloadVersion == source.StoredPayloadVersion
                    && supplied.Payload.AsSpan().SequenceEqual(source.Payload);
                _candidateConflict |= !_candidateFound;
            }
            else if (item.MessageId == candidate.MessageId) _messageReused = true;
        }
        if (viewKey is null || viewCutSequence is { } cut && item.Sequence > cut) return true;
        if (item.Event is null && _checkpoint is not null)
        {
            if (_checkpoint.MessageIds[_checkpoint.Sequence] == item.MessageId) return false;
            // Pure rejections change only the bounded source boundary. Preserve selected views
            // and defer checksum materialization until the next state-changing domain fold.
            _checkpoint = _checkpoint with
            {
                Sequence = item.Sequence,
                SourceDigest = _sourceDigest,
                ObservedAt = item.Timestamp > _checkpoint.ObservedAt ? item.Timestamp : _checkpoint.ObservedAt,
                EventDigests = new Dictionary<long, string> { [item.Sequence] = item.Digest },
                MessageIds = new Dictionary<long, string> { [item.Sequence] = item.MessageId },
            };
            _checkpointNeedsIntegrity = true;
            return true;
        }
        if (_checkpointNeedsIntegrity)
        {
            _checkpoint = _checkpoint! with { IntegrityDigest = OperationalStatusIntegrity.Checkpoint(_checkpoint!) };
            _checkpointNeedsIntegrity = false;
        }
        OperationalStatusFoldResult next = OperationalStatusFold.Apply(tenantId, folderId, _checkpoint, [item], bounded: true);
        foreach ((string taskId, TaskStatusReadModelSnapshot proposed) in next.Tasks)
        {
            if (OperationalStatusKeys.Task(tenantId, folderId, taskId) != viewKey) continue;
            var closed = next.Checkpoint.State.TaskClosures.Values.Where(value => value.TaskId == taskId)
                .OrderBy(value => next.Checkpoint.TaskClosedSequences[Hexalith.Folders.Aggregates.Folder.FolderState.TaskBindingKey(value.WorkspaceId, taskId)])
                .FirstOrDefault();
            if (closed is not null)
            {
                long position = next.Checkpoint.TaskClosedSequences[Hexalith.Folders.Aggregates.Folder.FolderState.TaskBindingKey(closed.WorkspaceId, taskId)];
                if (_completion is null || position < _completion.Sequence)
                    _completion = OperationalStatusTaskCompletion.Create(tenantId, folderId, closed, position, next.Checkpoint.SourceDigest);
            }
            TaskStatusReadModelSnapshot selected = _completion?.Restore(proposed) ?? proposed;
            _valueDigest = OperationalStatusIntegrity.Digest(selected);
            _viewSequence = long.Parse(selected.Freshness.ProjectionWatermark!, CultureInfo.InvariantCulture);
            _viewPrefix = _completion?.SourceDigest ?? next.Checkpoint.SourceDigest;
        }
        foreach ((string workspaceId, OperationalWorkspaceStatus workspace) in next.Workspaces)
        {
            if (OperationalStatusKeys.Workspace(tenantId, folderId, workspaceId) != viewKey) continue;
            _valueDigest = OperationalStatusIntegrity.Digest(workspace);
            _viewSequence = workspace.Sequence;
            _viewPrefix = workspace.SourceDigest;
        }
        _checkpoint = OperationalStatusCompaction.Compact(next.Checkpoint);
        return true;
    }

    /// <inheritdoc/>
    public OperationalStatusNativeSourceFacts Complete()
        => new(_sequence, _sourceDigest, _checkpointSourceDigest, _valueDigest, _viewSequence, _viewPrefix, _candidateFound, _messageReused, _candidateConflict);
}
