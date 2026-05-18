namespace Hexalith.Folders.Aggregates.Organization;

public sealed record OrganizationState(
    bool IsInitialized,
    IReadOnlySet<OrganizationAclEntryKey> Grants,
    IReadOnlyDictionary<string, string> IdempotencyFingerprints)
{
    public static OrganizationState Empty { get; } = new(
        false,
        new HashSet<OrganizationAclEntryKey>(),
        new Dictionary<string, string>(StringComparer.Ordinal));

    public OrganizationState Apply(IEnumerable<IOrganizationAclEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        bool initialized = IsInitialized;
        HashSet<OrganizationAclEntryKey> grants = new(Grants);
        Dictionary<string, string> idempotency = new(IdempotencyFingerprints, StringComparer.Ordinal);

        foreach (IOrganizationAclEvent aclEvent in events)
        {
            if (!string.IsNullOrWhiteSpace(aclEvent.IdempotencyKey))
            {
                idempotency[aclEvent.IdempotencyKey] = aclEvent.IdempotencyFingerprint;
            }

            switch (aclEvent)
            {
                case OrganizationAclBaselineInitialized:
                    initialized = true;
                    break;
                case OrganizationAclPrincipalGranted granted:
                    grants.Add(Key(granted.ManagedTenantId, granted.OrganizationId, granted.PrincipalKind, granted.PrincipalId, granted.Action));
                    initialized = true;
                    break;
                case OrganizationAclPrincipalRevoked revoked:
                    grants.Remove(Key(revoked.ManagedTenantId, revoked.OrganizationId, revoked.PrincipalKind, revoked.PrincipalId, revoked.Action));
                    initialized = true;
                    break;
            }
        }

        return new OrganizationState(initialized, grants, idempotency);
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
