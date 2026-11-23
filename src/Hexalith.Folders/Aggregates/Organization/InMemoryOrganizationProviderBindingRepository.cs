using System.Diagnostics;

namespace Hexalith.Folders.Aggregates.Organization;

/// <summary>
/// In-memory <see cref="IOrganizationProviderBindingRepository"/> for dev/test hosts. Production hosts
/// must register an EventStore-backed implementation.
/// This store keeps organization state in process memory until the host restarts.
/// </summary>
/// <remarks>
/// An append publishes the folded state, idempotency fingerprint and event count under one lock.
/// Each read acquires that lock. Separate read calls may observe different completed appends.
/// </remarks>
public sealed class InMemoryOrganizationProviderBindingRepository : IOrganizationProviderBindingRepository
{
    private readonly Dictionary<string, string> _idempotencyFingerprints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OrganizationState> _states = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private long _eventsAppended;

    /// <summary>Gets the number of appended events for test diagnostics.</summary>
    internal long EventsAppended
    {
        get
        {
            lock (_gate)
            {
                return _eventsAppended;
            }
        }
    }

    /// <inheritdoc/>
    public OrganizationStreamName CreateStreamName(string managedTenantId, string organizationId)
        => OrganizationStreamName.Create(managedTenantId, organizationId);

    /// <inheritdoc/>
    public OrganizationState Load(OrganizationStreamName streamName)
    {
        ArgumentNullException.ThrowIfNull(streamName);

        lock (_gate)
        {
            return LoadUnderGate(streamName);
        }
    }

    /// <inheritdoc/>
    public OrganizationAclAppendOutcome AppendIfFingerprintAbsent(
        OrganizationStreamName streamName,
        string idempotencyKey,
        string fingerprint,
        IReadOnlyList<IOrganizationEvent> events)
    {
        ArgumentNullException.ThrowIfNull(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentNullException.ThrowIfNull(events);

        lock (_gate)
        {
            string ledgerKey = LedgerKey(streamName, idempotencyKey);
            // A replay needs only the non-empty check. For a new key, take a stable batch before
            // the final ledger lookup because caller-controlled enumeration can reenter append.
            IReadOnlyList<IOrganizationEvent> batch = _idempotencyFingerprints.ContainsKey(ledgerKey) ? events : events.ToArray();
            int eventCount = batch.Count;
            if (eventCount == 0)
            {
                throw new ArgumentException("Append requires at least one event.", nameof(events));
            }

            if (_idempotencyFingerprints.TryGetValue(ledgerKey, out string? priorFingerprint))
            {
                return string.Equals(priorFingerprint, fingerprint, StringComparison.Ordinal)
                    ? OrganizationAclAppendOutcome.FingerprintMatched
                    : OrganizationAclAppendOutcome.FingerprintConflict;
            }

            OrganizationState next = LoadUnderGate(streamName).Apply(batch);
            _states[streamName.Value] = next;
            _idempotencyFingerprints[ledgerKey] = fingerprint;
            _eventsAppended += eventCount;
            return OrganizationAclAppendOutcome.Appended;
        }
    }

    /// <inheritdoc/>
    public bool TryGetIdempotencyFingerprint(
        OrganizationStreamName streamName,
        string idempotencyKey,
        out string? fingerprint)
    {
        ArgumentNullException.ThrowIfNull(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        lock (_gate)
        {
            return _idempotencyFingerprints.TryGetValue(LedgerKey(streamName, idempotencyKey), out fingerprint);
        }
    }

    private OrganizationState LoadUnderGate(OrganizationStreamName streamName)
    {
        Debug.Assert(Monitor.IsEntered(_gate));
        return _states.GetValueOrDefault(streamName.Value, OrganizationState.Empty);
    }

    private static string LedgerKey(OrganizationStreamName streamName, string idempotencyKey)
        => $"{streamName.Value}|{idempotencyKey}";
}
