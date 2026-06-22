using System.Collections.Concurrent;

namespace Hexalith.Folders.Aggregates.Organization;

/// <summary>
/// In-memory <see cref="IOrganizationProviderBindingRepository"/> for dev/test hosts. Production hosts
/// MUST register an EventStore-backed implementation instead (mirrors the folder repository policy):
/// this store keeps organization state in process memory and does not survive a restart.
/// </summary>
public sealed class InMemoryOrganizationProviderBindingRepository : IOrganizationProviderBindingRepository
{
    private readonly ConcurrentDictionary<string, string> _idempotencyFingerprints = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, OrganizationState> _states = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    // Internal test affordance visible to the test assemblies via the InternalsVisibleTo declared in
    // InMemoryFolderRepository.cs (assembly-level).
    internal int EventsAppended { get; private set; }

    /// <inheritdoc/>
    public OrganizationStreamName CreateStreamName(string managedTenantId, string organizationId)
        => OrganizationStreamName.Create(managedTenantId, organizationId);

    /// <inheritdoc/>
    public OrganizationState Load(OrganizationStreamName streamName)
    {
        ArgumentNullException.ThrowIfNull(streamName);

        return _states.TryGetValue(streamName.Value, out OrganizationState? state)
            ? state
            : OrganizationState.Empty;
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
        if (events.Count == 0)
        {
            throw new ArgumentException("Append requires at least one event.", nameof(events));
        }

        lock (_gate)
        {
            string ledgerKey = LedgerKey(streamName, idempotencyKey);
            if (_idempotencyFingerprints.TryGetValue(ledgerKey, out string? priorFingerprint))
            {
                return string.Equals(priorFingerprint, fingerprint, StringComparison.Ordinal)
                    ? OrganizationAclAppendOutcome.FingerprintMatched
                    : OrganizationAclAppendOutcome.FingerprintConflict;
            }

            OrganizationState next = Load(streamName).Apply(events);
            _states[streamName.Value] = next;
            _idempotencyFingerprints[ledgerKey] = fingerprint;
            EventsAppended += events.Count;
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

        return _idempotencyFingerprints.TryGetValue(LedgerKey(streamName, idempotencyKey), out fingerprint);
    }

    private static string LedgerKey(OrganizationStreamName streamName, string idempotencyKey)
        => $"{streamName.Value}|{idempotencyKey}";
}
