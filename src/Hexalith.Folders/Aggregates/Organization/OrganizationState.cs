using System.Collections.Frozen;

namespace Hexalith.Folders.Aggregates.Organization;

public sealed record OrganizationState(
    bool IsInitialized,
    IReadOnlySet<OrganizationAclEntryKey> Grants,
    IReadOnlyDictionary<string, string> IdempotencyFingerprints,
    IReadOnlyDictionary<string, OrganizationProviderBinding> ProviderBindings)
{
    public static OrganizationState Empty { get; } = new(
        false,
        FrozenSet<OrganizationAclEntryKey>.Empty,
        FrozenDictionary<string, string>.Empty,
        FrozenDictionary<string, OrganizationProviderBinding>.Empty);

    public OrganizationState Apply(IEnumerable<IOrganizationEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        bool initialized = IsInitialized;
        HashSet<OrganizationAclEntryKey> grants = new(Grants);
        Dictionary<string, string> idempotency = new(IdempotencyFingerprints, StringComparer.Ordinal);
        Dictionary<string, OrganizationProviderBinding> providerBindings = new(ProviderBindings, StringComparer.Ordinal);
        string? expectedTenantId = null;
        string? expectedOrganizationId = null;

        foreach (IOrganizationEvent organizationEvent in events)
        {
            expectedTenantId ??= organizationEvent.ManagedTenantId;
            expectedOrganizationId ??= organizationEvent.OrganizationId;

            if (!string.Equals(expectedTenantId, organizationEvent.ManagedTenantId, StringComparison.Ordinal)
                || !string.Equals(expectedOrganizationId, organizationEvent.OrganizationId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Foreign event tenant/organization in Apply: expected {expectedTenantId}:organizations:{expectedOrganizationId}, " +
                    $"got {organizationEvent.ManagedTenantId}:organizations:{organizationEvent.OrganizationId}.");
            }

            if (!string.IsNullOrWhiteSpace(organizationEvent.IdempotencyKey))
            {
                idempotency[organizationEvent.IdempotencyKey] = organizationEvent.IdempotencyFingerprint;
            }

            switch (organizationEvent)
            {
                case OrganizationAclBaselineInitialized when !initialized:
                    initialized = true;
                    break;
                case OrganizationAclPrincipalGranted granted when initialized:
                    grants.Add(Key(granted.ManagedTenantId, granted.OrganizationId, granted.PrincipalKind, granted.PrincipalId, granted.Action));
                    break;
                case OrganizationAclPrincipalRevoked revoked when initialized:
                    grants.Remove(Key(revoked.ManagedTenantId, revoked.OrganizationId, revoked.PrincipalKind, revoked.PrincipalId, revoked.Action));
                    break;
                case OrganizationAclBaselineInitialized:
                    throw new InvalidOperationException("Organization baseline is already initialized.");
                case OrganizationAclPrincipalGranted or OrganizationAclPrincipalRevoked:
                    throw new InvalidOperationException("Organization ACL event requires an initialized baseline.");
                case ProviderBindingConfigured configured:
                    providerBindings[configured.ProviderBindingRef] = new OrganizationProviderBinding(
                        configured.ManagedTenantId,
                        configured.OrganizationId,
                        configured.ProviderBindingRef,
                        configured.ProviderKind,
                        configured.CredentialReferenceId,
                        configured.NamingPolicy,
                        configured.BranchPolicy,
                        configured.CorrelationId,
                        configured.TaskId,
                        configured.IdempotencyKey,
                        configured.IdempotencyFingerprint,
                        configured.ConfiguredStatus,
                        configured.OccurredAt);
                    initialized = true;
                    break;
                default:
                    throw new InvalidOperationException("Unknown organization event in replay.");
            }
        }

        return new OrganizationState(
            initialized,
            grants.ToFrozenSet(),
            idempotency.ToFrozenDictionary(StringComparer.Ordinal),
            providerBindings.ToFrozenDictionary(StringComparer.Ordinal));
    }

    public bool HasPermission(
        string managedTenantId,
        string organizationId,
        OrganizationAclPrincipalKind principalKind,
        string principalId,
        string action)
        => Grants.Contains(Key(managedTenantId, organizationId, principalKind, principalId, action));

    internal bool HasGrant(OrganizationAclEntryKey key) => Grants.Contains(key);

    private static OrganizationAclEntryKey Key(
        string managedTenantId,
        string organizationId,
        OrganizationAclPrincipalKind principalKind,
        string principalId,
        string action)
        => new(managedTenantId, organizationId, principalKind, principalId, action);
}
