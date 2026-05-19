using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

internal sealed class RecordingFolderRepository : IFolderRepository
{
    private readonly Dictionary<string, string> _idempotencyFingerprints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FolderState> _states = new(StringComparer.Ordinal);

    public int AuditResourcesQueried { get; private set; }

    public int DiagnosticsQueried { get; private set; }

    public int EventsAppended { get; private set; }

    public int ProviderReadinessChecked { get; private set; }

    public int RepositoriesCreated { get; private set; }

    public int StreamsLoaded { get; private set; }

    public int StreamNamesConstructed { get; private set; }

    public int IdempotencyLookups { get; private set; }

    public int AppendsAttempted { get; private set; }

    public bool IdempotencyUnavailable { get; set; }

    public bool SimulateAppendConflict { get; set; }

    public string? LastDurableKey { get; private set; }

    public string? LastStreamName { get; private set; }

    public IReadOnlyList<IFolderEvent> LastAppendedEvents { get; private set; } = [];

    public FolderState Load(FolderStreamName streamName)
    {
        StreamsLoaded++;
        LastStreamName = streamName.Value;
        return _states.TryGetValue(streamName.Value, out FolderState? state) ? state : FolderState.Empty;
    }

    public FolderStreamName CreateStreamName(string managedTenantId, string folderId)
    {
        StreamNamesConstructed++;
        FolderStreamName streamName = FolderStreamName.Create(managedTenantId, folderId);
        LastStreamName = streamName.Value;
        return streamName;
    }

    public FolderAppendOutcome AppendIfFingerprintAbsent(
        FolderStreamName streamName,
        string idempotencyKey,
        string fingerprint,
        IReadOnlyList<IFolderEvent> events)
    {
        AppendsAttempted++;
        string ledgerKey = LedgerKey(streamName, idempotencyKey);

        if (_idempotencyFingerprints.TryGetValue(ledgerKey, out string? priorFingerprint))
        {
            // Do NOT set LastDurableKey on a fingerprint mismatch/match short-circuit:
            // the assertion `LastDurableKey.ShouldBeNull()` on side-effect-rejected paths
            // is meaningful only if successful-append intent is the only thing that sets it.
            return string.Equals(priorFingerprint, fingerprint, StringComparison.Ordinal)
                ? FolderAppendOutcome.FingerprintMatched
                : FolderAppendOutcome.FingerprintConflict;
        }

        if (SimulateAppendConflict)
        {
            return FolderAppendOutcome.AppendConflict;
        }

        EventsAppended += events.Count;
        LastStreamName = streamName.Value;
        LastDurableKey = ledgerKey;
        LastAppendedEvents = events;
        _states[streamName.Value] = Load(streamName).Apply(events, streamName);
        _idempotencyFingerprints[ledgerKey] = fingerprint;
        return FolderAppendOutcome.Appended;
    }

    public FolderIdempotencyLookupResult TryGetIdempotencyFingerprint(
        FolderStreamName streamName,
        string idempotencyKey,
        out string? fingerprint)
    {
        IdempotencyLookups++;
        fingerprint = null;
        if (IdempotencyUnavailable)
        {
            return FolderIdempotencyLookupResult.Unavailable;
        }

        string ledgerKey = LedgerKey(streamName, idempotencyKey);
        return _idempotencyFingerprints.TryGetValue(ledgerKey, out fingerprint)
            ? FolderIdempotencyLookupResult.Found
            : FolderIdempotencyLookupResult.Missing;
    }

    public void Seed(FolderStreamName streamName, IReadOnlyList<IFolderEvent> events)
        => _states[streamName.Value] = FolderState.Empty.Apply(events, streamName);

    public void RecordIdempotency(string managedTenantId, string folderId, string idempotencyKey, string fingerprint)
    {
        FolderStreamName streamName = FolderStreamName.Create(managedTenantId, folderId);
        _idempotencyFingerprints[LedgerKey(streamName, idempotencyKey)] = fingerprint;
    }

    // Single ledger-key shape: stream-name + idempotency key. Production impls and the
    // test spy address the ledger under the same identity, so a real adapter cannot
    // diverge from the test contract by reading or writing under a different key.
    private static string LedgerKey(FolderStreamName streamName, string idempotencyKey)
        => $"{streamName.Value}|{idempotencyKey}";
}
