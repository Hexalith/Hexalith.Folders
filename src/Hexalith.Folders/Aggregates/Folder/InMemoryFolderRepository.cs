using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Hexalith.Folders.Queries.Folders;

[assembly: InternalsVisibleTo("Hexalith.Folders.Tests")]
[assembly: InternalsVisibleTo("Hexalith.Folders.IntegrationTests")]
[assembly: InternalsVisibleTo("Hexalith.Folders.Server.Tests")]

namespace Hexalith.Folders.Aggregates.Folder;

public sealed class InMemoryFolderRepository : IFolderRepository
{
    private readonly ConcurrentDictionary<string, string> _idempotencyFingerprints = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, FolderState> _states = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastObservedAt = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly InMemoryFolderLifecycleStatusReadModel? _lifecycleReadModel;
    private readonly TimeProvider _timeProvider;

    public InMemoryFolderRepository(
        IFolderLifecycleStatusReadModel? lifecycleReadModel = null,
        TimeProvider? timeProvider = null)
    {
        // Lifecycle snapshot writes go through the concrete in-memory read-model. Fail loud
        // if a different IFolderLifecycleStatusReadModel implementation was injected so the
        // wiring drift is visible at startup rather than silently dropping snapshots.
        if (lifecycleReadModel is not null && lifecycleReadModel is not InMemoryFolderLifecycleStatusReadModel)
        {
            throw new ArgumentException(
                $"InMemoryFolderRepository requires {nameof(InMemoryFolderLifecycleStatusReadModel)}; received {lifecycleReadModel.GetType().Name}.",
                nameof(lifecycleReadModel));
        }

        _lifecycleReadModel = (InMemoryFolderLifecycleStatusReadModel?)lifecycleReadModel;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    // Internal test affordance: read-only counter visible to test assemblies via
    // InternalsVisibleTo. Production code cannot see this property.
    internal int EventsAppended { get; private set; }

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
        if (events.Count == 0)
        {
            // Recording an empty-events fingerprint would let a later distinct command with
            // the same idempotency key falsely conflict against a "no-op" ledger entry.
            throw new ArgumentException("Append requires at least one event.", nameof(events));
        }

        // Capture the observation time before taking the lock so it reflects the request's
        // arrival ordering rather than the lock-acquisition ordering. Combined with the
        // per-folder monotonic clamp in SaveLifecycleSnapshot, this keeps the projection's
        // freshness watermark non-decreasing across concurrent writers.
        DateTimeOffset observedAt = _timeProvider.GetUtcNow();

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
            SaveLifecycleSnapshot(next, observedAt);
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

        DateTimeOffset observedAt = _timeProvider.GetUtcNow();

        lock (_gate)
        {
            // Symmetric duplicate-seed guard: throw if the stream already has materialized
            // state. Fixture authors get fail-loud feedback on accidental double-seed of the
            // state map (the ledger map below is guarded by the same fail-loud contract).
            if (_states.ContainsKey(streamName.Value))
            {
                throw new InvalidOperationException(
                    $"Seed would overwrite an existing state for stream '{streamName.Value}'. Compose all seeded events into a single Seed call per stream.");
            }

            FolderState seeded = FolderState.Empty.Apply(events, streamName);
            _states[streamName.Value] = seeded;
            foreach (IFolderEvent folderEvent in events)
            {
                if (string.IsNullOrWhiteSpace(folderEvent.IdempotencyKey))
                {
                    continue;
                }

                string ledgerKey = LedgerKey(streamName, folderEvent.IdempotencyKey);
                // Silent overwrite of an existing ledger entry would mask real idempotency
                // races; require seed callers to use unique keys per stream.
                if (_idempotencyFingerprints.ContainsKey(ledgerKey))
                {
                    throw new InvalidOperationException(
                        $"Seed would overwrite an existing idempotency ledger entry for stream '{streamName.Value}' and key '{folderEvent.IdempotencyKey}'.");
                }

                _idempotencyFingerprints[ledgerKey] = folderEvent.IdempotencyFingerprint;
            }

            SaveLifecycleSnapshot(seeded, observedAt);
        }
    }

    // Internal test affordance — production code cannot see this method (InternalsVisibleTo
    // exposes it only to the test assemblies). Resets the appended-event counter so a test
    // fixture can assert deltas across phases without splitting state between instances.
    internal void ResetAppendCounters() => EventsAppended = 0;

    private static string LedgerKey(FolderStreamName streamName, string idempotencyKey)
        => $"{streamName.Value}|{idempotencyKey}";

    private void SaveLifecycleSnapshot(FolderState state, DateTimeOffset observedAt)
    {
        if (_lifecycleReadModel is null
            || !state.IsCreated
            || string.IsNullOrWhiteSpace(state.ManagedTenantId)
            || string.IsNullOrWhiteSpace(state.FolderId))
        {
            return;
        }

        // For an archived folder we know exactly when the transition occurred; for any
        // other state we use the caller-supplied observation time so the projection's
        // freshness reflects actual write time rather than a sentinel epoch zero.
        DateTimeOffset rawObservedAt = state.ArchivedAt ?? observedAt;

        // Clamp per-folder so a slow clock or a test-time fake cannot move the watermark
        // backwards across concurrent writers. The monotonic guarantee is per-stream, not
        // global; that matches the freshness contract consumers rely on.
        string folderKey = LifecycleKey(state.ManagedTenantId, state.FolderId);
        DateTimeOffset clamped = _lastObservedAt.AddOrUpdate(
            folderKey,
            rawObservedAt,
            (_, previous) => previous > rawObservedAt ? previous : rawObservedAt);

        _lifecycleReadModel.Save(new FolderLifecycleStatusReadModelSnapshot(
            state.ManagedTenantId,
            state.FolderId,
            state.LifecycleState == FolderLifecycleState.Archived
                ? FolderLifecycleProjectionState.Archived
                : FolderLifecycleProjectionState.Active,
            FolderRepositoryBindingStatus.Unbound,
            RepositoryBindingId: null,
            ProviderBindingRef: null,
            new FolderLifecycleFreshness("eventually_consistent", clamped, "in-memory-folder-repository", Stale: false, ReasonCode: null),
            new FolderLifecycleEvidenceScope(
                state.ManagedTenantId,
                state.ArchiveActorPrincipalId,
                "read_metadata",
                state.ArchiveTaskId,
                state.ArchiveCorrelationId,
                "in-memory-folder-repository"),
            []));
    }

    private static string LifecycleKey(string managedTenantId, string folderId)
        => $"{managedTenantId}|{folderId}";
}
