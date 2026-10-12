using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.Server;

/// <summary>Reads the authorized provider-binding actor stream without a separate write store.</summary>
internal sealed class ActorFoldedOrganizationProviderBindingRepository(FolderEventReplay replay)
    : IOrganizationProviderBindingRepository
{
    private string? _tenantId;
    private string? _aggregateId;
    private OrganizationState? _state;

    /// <summary>Admits the actor-owned state for one authorized command.</summary>
    internal void Begin(string tenantId, string aggregateId, object? currentState)
    {
        if (_state is not null)
        {
            throw new InvalidOperationException("The actor state was already admitted.");
        }

        _tenantId = tenantId;
        _aggregateId = aggregateId;
        _state = replay.FoldOrganization(currentState, tenantId, aggregateId);
    }

    /// <inheritdoc/>
    public OrganizationStreamName CreateStreamName(string managedTenantId, string organizationId)
        => OrganizationStreamName.Create(managedTenantId, organizationId);

    /// <inheritdoc/>
    public OrganizationState Load(OrganizationStreamName streamName)
    {
        RequireStream(streamName);
        return _state!;
    }

    /// <inheritdoc/>
    public OrganizationAclAppendOutcome AppendIfFingerprintAbsent(OrganizationStreamName streamName,
        string idempotencyKey, string fingerprint, IReadOnlyList<IOrganizationEvent> events)
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
                ? OrganizationAclAppendOutcome.FingerprintMatched : OrganizationAclAppendOutcome.FingerprintConflict;
        }

        foreach (IOrganizationEvent item in events)
        {
            if (!string.Equals(item.ManagedTenantId, _tenantId, StringComparison.Ordinal)
                || item is not ProviderBindingConfigured binding
                || !string.Equals(binding.ProviderBindingRef, _aggregateId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Organization event targets another actor stream.");
            }
        }

        _ = _state.Apply(events);
        return OrganizationAclAppendOutcome.Appended;
    }

    /// <inheritdoc/>
    public bool TryGetIdempotencyFingerprint(OrganizationStreamName streamName,
        string idempotencyKey, out string? fingerprint)
    {
        RequireStream(streamName);
        return _state!.IdempotencyFingerprints.TryGetValue(idempotencyKey, out fingerprint);
    }

    private void RequireStream(OrganizationStreamName streamName)
    {
        ArgumentNullException.ThrowIfNull(streamName);
        if (_state is null || _tenantId is null
            || !streamName.Value.StartsWith($"{_tenantId}:organizations:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Organization read is outside the authorized actor stream.");
        }

        OrganizationProviderBinding? existing = _state.ProviderBindings.Values.FirstOrDefault();
        if (existing is not null
            && !string.Equals(streamName.Value,
                OrganizationStreamName.Create(_tenantId, existing.OrganizationId).Value,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Organization read targets another actor stream.");
        }
    }
}
