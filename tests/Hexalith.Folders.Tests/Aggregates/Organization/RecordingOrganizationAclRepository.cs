using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

internal sealed class RecordingOrganizationAclRepository : IOrganizationAclRepository
{
    private readonly Dictionary<string, string> _idempotencyFingerprints = new(StringComparer.Ordinal);
    private OrganizationState _state = OrganizationState.Empty;

    public int AuditResourcesQueried { get; private set; }

    public int DiagnosticsQueried { get; private set; }

    public int EventsAppended { get; private set; }

    public int StreamsLoaded { get; private set; }

    public int StreamNamesConstructed { get; private set; }

    public string? LastStreamName { get; private set; }

    public OrganizationState Load(OrganizationStreamName streamName)
    {
        StreamsLoaded++;
        LastStreamName = streamName.Value;
        return _state;
    }

    public OrganizationStreamName CreateStreamName(string managedTenantId, string organizationId)
    {
        StreamNamesConstructed++;
        OrganizationStreamName streamName = OrganizationStreamName.Create(managedTenantId, organizationId);
        LastStreamName = streamName.Value;
        return streamName;
    }

    public OrganizationAclAppendOutcome AppendIfFingerprintAbsent(
        OrganizationStreamName streamName,
        string idempotencyKey,
        string fingerprint,
        IReadOnlyList<IOrganizationAclEvent> events)
    {
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
        string managedTenantId,
        string organizationId,
        string idempotencyKey,
        out string? fingerprint)
        => _idempotencyFingerprints.TryGetValue(
            LedgerKey(managedTenantId, organizationId, idempotencyKey),
            out fingerprint);

    public void RecordIdempotency(string managedTenantId, string organizationId, string idempotencyKey, string fingerprint)
        => _idempotencyFingerprints[LedgerKey(managedTenantId, organizationId, idempotencyKey)] = fingerprint;

    private static string LedgerKey(string managedTenantId, string organizationId, string idempotencyKey)
        => $"{managedTenantId}|{organizationId}|{idempotencyKey}";

    private static string LedgerKey(OrganizationStreamName streamName, string idempotencyKey)
        => $"{streamName.Value}|{idempotencyKey}";
}
