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
        LastDurableKey = LedgerKey(streamName.Value, idempotencyKey);
        string ledgerKey = LedgerKey(streamName.Value, idempotencyKey);
        if (_idempotencyFingerprints.TryGetValue(ledgerKey, out string? priorFingerprint))
        {
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
        LastAppendedEvents = events;
        _states[streamName.Value] = Load(streamName).Apply(events);
        _idempotencyFingerprints[ledgerKey] = fingerprint;
        return FolderAppendOutcome.Appended;
    }

    public FolderIdempotencyLookupResult TryGetIdempotencyFingerprint(
        string managedTenantId,
        string folderId,
        string idempotencyKey,
        out string? fingerprint)
    {
        fingerprint = null;
        if (IdempotencyUnavailable)
        {
            return FolderIdempotencyLookupResult.Unavailable;
        }

        LastDurableKey = LedgerKey(managedTenantId, folderId, idempotencyKey);
        return _idempotencyFingerprints.TryGetValue(LastDurableKey, out fingerprint)
            ? FolderIdempotencyLookupResult.Found
            : FolderIdempotencyLookupResult.Missing;
    }

    public void Seed(FolderStreamName streamName, IReadOnlyList<IFolderEvent> events)
        => _states[streamName.Value] = FolderState.Empty.Apply(events);

    public void RecordIdempotency(string managedTenantId, string folderId, string idempotencyKey, string fingerprint)
        => _idempotencyFingerprints[LedgerKey(managedTenantId, folderId, idempotencyKey)] = fingerprint;

    private static string LedgerKey(string managedTenantId, string folderId, string idempotencyKey)
        => $"{managedTenantId}:folders:{folderId}|{idempotencyKey}";

    private static string LedgerKey(string streamName, string idempotencyKey)
        => $"{streamName}|{idempotencyKey}";
}
