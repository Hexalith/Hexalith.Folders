using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

internal sealed class RecordingOrganizationProviderBindingRepository : IOrganizationProviderBindingRepository
{
    private readonly Dictionary<string, string> _idempotencyFingerprints = new(StringComparer.Ordinal);
    private OrganizationState _state;

    public RecordingOrganizationProviderBindingRepository(OrganizationState? state = null)
    {
        _state = state ?? OrganizationState.Empty;
    }

    public int EventsAppended { get; private set; }

    public int IdempotencyLookups { get; private set; }

    public int StreamsLoaded { get; private set; }

    public int StreamNamesConstructed { get; private set; }

    public string? LastStreamName { get; private set; }

    public OrganizationAclAppendOutcome? ForcedAppendOutcome { get; set; }

    public OrganizationStreamName CreateStreamName(string managedTenantId, string organizationId)
    {
        StreamNamesConstructed++;
        OrganizationStreamName streamName = OrganizationStreamName.Create(managedTenantId, organizationId);
        LastStreamName = streamName.Value;
        return streamName;
    }

    public OrganizationState Load(OrganizationStreamName streamName)
    {
        StreamsLoaded++;
        LastStreamName = streamName.Value;
        return _state;
    }

    public OrganizationAclAppendOutcome AppendIfFingerprintAbsent(
        OrganizationStreamName streamName,
        string idempotencyKey,
        string fingerprint,
        IReadOnlyList<IOrganizationEvent> events)
    {
        if (ForcedAppendOutcome is { } forced)
        {
            LastStreamName = streamName.Value;
            return forced;
        }

        string ledgerKey = LedgerKey(streamName, idempotencyKey);
        if (_idempotencyFingerprints.TryGetValue(ledgerKey, out string? priorFingerprint))
        {
            return string.Equals(priorFingerprint, fingerprint, StringComparison.Ordinal)
                ? OrganizationAclAppendOutcome.FingerprintMatched
                : OrganizationAclAppendOutcome.FingerprintConflict;
        }

        EventsAppended += events.Count;
        LastStreamName = streamName.Value;
        _state = _state.Apply(events);
        _idempotencyFingerprints[ledgerKey] = fingerprint;
        return OrganizationAclAppendOutcome.Appended;
    }

    public bool TryGetIdempotencyFingerprint(
        OrganizationStreamName streamName,
        string idempotencyKey,
        out string? fingerprint)
    {
        IdempotencyLookups++;
        return _idempotencyFingerprints.TryGetValue(LedgerKey(streamName, idempotencyKey), out fingerprint);
    }

    public void RecordIdempotency(OrganizationStreamName streamName, string idempotencyKey, string fingerprint)
        => _idempotencyFingerprints[LedgerKey(streamName, idempotencyKey)] = fingerprint;

    private static string LedgerKey(OrganizationStreamName streamName, string idempotencyKey)
        => $"{streamName.Value}|{idempotencyKey}";
}
