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

    public void Append(OrganizationStreamName streamName, IReadOnlyList<IOrganizationAclEvent> events)
    {
        EventsAppended += events.Count;
        LastStreamName = streamName.Value;
        _state = _state.Apply(events);

        foreach (IOrganizationAclEvent aclEvent in events)
        {
            _idempotencyFingerprints[LedgerKey(aclEvent.ManagedTenantId, aclEvent.OrganizationId, aclEvent.IdempotencyKey)] =
                aclEvent.IdempotencyFingerprint;
        }
    }

    public bool TryGetIdempotencyFingerprint(
        string managedTenantId,
        string organizationId,
        string idempotencyKey,
        out string? fingerprint)
        => _idempotencyFingerprints.TryGetValue(LedgerKey(managedTenantId, organizationId, idempotencyKey), out fingerprint);

    public void RecordIdempotency(string managedTenantId, string organizationId, string idempotencyKey, string fingerprint)
        => _idempotencyFingerprints[LedgerKey(managedTenantId, organizationId, idempotencyKey)] = fingerprint;

    private static string LedgerKey(string managedTenantId, string organizationId, string idempotencyKey)
        => $"{managedTenantId}|{organizationId}|{idempotencyKey}";
}
