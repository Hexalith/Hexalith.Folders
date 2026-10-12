using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Server;

/// <summary>Provides a command-local view of the EventStore actor stream to existing folder services.</summary>
internal sealed class ActorFoldedFolderRepository(FolderEventReplay replay) : IFolderRepository
{
    private FolderStreamName? _stream;
    private FolderState? _state;

    /// <summary>Admits the actor-owned state for one authorized command.</summary>
    internal void Begin(string tenantId, string aggregateId, object? currentState)
    {
        if (_stream is not null)
        {
            throw new InvalidOperationException("The actor state was already admitted.");
        }

        _stream = FolderStreamName.Create(tenantId, aggregateId);
        _state = replay.FoldFolder(currentState, tenantId, aggregateId);
    }

    /// <inheritdoc/>
    public FolderStreamName CreateStreamName(string managedTenantId, string folderId)
        => FolderStreamName.Create(managedTenantId, folderId);

    /// <inheritdoc/>
    public FolderState Load(FolderStreamName streamName)
    {
        RequireStream(streamName);
        return _state!;
    }

    /// <inheritdoc/>
    public FolderAppendOutcome AppendIfFingerprintAbsent(FolderStreamName streamName,
        string idempotencyKey, string fingerprint, IReadOnlyList<IFolderEvent> events)
    {
        RequireStream(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
        {
            throw new ArgumentException("An accepted decision needs events.", nameof(events));
        }

        if (_state!.IdempotencyFingerprints.TryGetValue(idempotencyKey, out string? existing))
        {
            return string.Equals(existing, fingerprint, StringComparison.Ordinal)
                ? FolderAppendOutcome.FingerprintMatched : FolderAppendOutcome.FingerprintConflict;
        }

        // Preflight the fold. EventStore owns the only write after this process result is returned.
        _ = _state.Apply(events, streamName);
        return FolderAppendOutcome.Appended;
    }

    /// <inheritdoc/>
    public FolderIdempotencyLookupResult TryGetIdempotencyFingerprint(FolderStreamName streamName,
        string idempotencyKey, out string? fingerprint)
    {
        RequireStream(streamName);
        bool found = _state!.IdempotencyFingerprints.TryGetValue(idempotencyKey, out fingerprint);
        return found ? FolderIdempotencyLookupResult.Found : FolderIdempotencyLookupResult.Missing;
    }

    private void RequireStream(FolderStreamName streamName)
    {
        ArgumentNullException.ThrowIfNull(streamName);
        if (_stream is null || !string.Equals(streamName.Value, _stream.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Folder read is outside the authorized actor stream.");
        }
    }
}
