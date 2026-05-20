using System.Collections.Concurrent;

using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed class InMemoryFolderRepository(IFolderLifecycleStatusReadModel? lifecycleReadModel = null) : IFolderRepository
{
    private readonly ConcurrentDictionary<string, string> _idempotencyFingerprints = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, FolderState> _states = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly InMemoryFolderLifecycleStatusReadModel? _lifecycleReadModel = lifecycleReadModel as InMemoryFolderLifecycleStatusReadModel;

    public int EventsAppended { get; private set; }

    public FolderStreamName CreateStreamName(string managedTenantId, string folderId)
        => FolderStreamName.Create(managedTenantId, folderId);

    public FolderState Load(FolderStreamName streamName)
    {
        ArgumentNullException.ThrowIfNull(streamName);

        return _states.TryGetValue(streamName.Value, out FolderState? state)
            ? state
            : FolderState.Empty;
    }

    public FolderAppendOutcome AppendIfFingerprintAbsent(
        FolderStreamName streamName,
        string idempotencyKey,
        string fingerprint,
        IReadOnlyList<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentNullException.ThrowIfNull(events);

        lock (_gate)
        {
            string ledgerKey = LedgerKey(streamName, idempotencyKey);
            if (_idempotencyFingerprints.TryGetValue(ledgerKey, out string? priorFingerprint))
            {
                return string.Equals(priorFingerprint, fingerprint, StringComparison.Ordinal)
                    ? FolderAppendOutcome.FingerprintMatched
                    : FolderAppendOutcome.FingerprintConflict;
            }

            FolderState current = Load(streamName);
            FolderState next = current.Apply(events, streamName);
            _states[streamName.Value] = next;
            _idempotencyFingerprints[ledgerKey] = fingerprint;
            EventsAppended += events.Count;
            SaveLifecycleSnapshot(next);
            return FolderAppendOutcome.Appended;
        }
    }

    public FolderIdempotencyLookupResult TryGetIdempotencyFingerprint(
        FolderStreamName streamName,
        string idempotencyKey,
        out string? fingerprint)
    {
        ArgumentNullException.ThrowIfNull(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return _idempotencyFingerprints.TryGetValue(LedgerKey(streamName, idempotencyKey), out fingerprint)
            ? FolderIdempotencyLookupResult.Found
            : FolderIdempotencyLookupResult.Missing;
    }

    public void Seed(FolderStreamName streamName, IReadOnlyList<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(streamName);
        ArgumentNullException.ThrowIfNull(events);

        lock (_gate)
        {
            FolderState seeded = FolderState.Empty.Apply(events, streamName);
            _states[streamName.Value] = seeded;
            foreach (IFolderEvent folderEvent in events)
            {
                if (!string.IsNullOrWhiteSpace(folderEvent.IdempotencyKey))
                {
                    _idempotencyFingerprints[LedgerKey(streamName, folderEvent.IdempotencyKey)] = folderEvent.IdempotencyFingerprint;
                }
            }

            SaveLifecycleSnapshot(seeded);
        }
    }

    public void ResetAppendCounters() => EventsAppended = 0;

    private static string LedgerKey(FolderStreamName streamName, string idempotencyKey)
        => $"{streamName.Value}|{idempotencyKey}";

    private void SaveLifecycleSnapshot(FolderState state)
    {
        if (_lifecycleReadModel is null
            || !state.IsCreated
            || string.IsNullOrWhiteSpace(state.ManagedTenantId)
            || string.IsNullOrWhiteSpace(state.FolderId))
        {
            return;
        }

        DateTimeOffset observedAt = state.ArchivedAt ?? DateTimeOffset.UnixEpoch;
        _lifecycleReadModel.Save(new FolderLifecycleStatusReadModelSnapshot(
            state.ManagedTenantId,
            state.FolderId,
            state.LifecycleState == FolderLifecycleState.Archived
                ? FolderLifecycleProjectionState.Archived
                : FolderLifecycleProjectionState.Active,
            FolderRepositoryBindingStatus.Unbound,
            RepositoryBindingId: null,
            ProviderBindingRef: null,
            new FolderLifecycleFreshness("eventually_consistent", observedAt, "in-memory-folder-repository", Stale: false, ReasonCode: null),
            new FolderLifecycleEvidenceScope(
                state.ManagedTenantId,
                state.ArchiveActorPrincipalId,
                "read_metadata",
                state.ArchiveTaskId,
                state.ArchiveCorrelationId,
                "in-memory-folder-repository"),
            []));
    }
}
