namespace Hexalith.Folders.Aggregates.Organization;

public sealed record OrganizationAclEntryKey(
    string ManagedTenantId,
    string OrganizationId,
    OrganizationAclPrincipalKind PrincipalKind,
    string PrincipalId,
    string Action)
{
    public string CanonicalValue
        => string.Join(
            "|",
            ManagedTenantId,
            OrganizationId,
            PrincipalKindToken,
            PrincipalId,
            Action);

    public string PrincipalKindToken
        => PrincipalKind switch
        {
            OrganizationAclPrincipalKind.User => "user",
            OrganizationAclPrincipalKind.Group => "group",
            OrganizationAclPrincipalKind.Role => "role",
            OrganizationAclPrincipalKind.DelegatedServiceAgent => "delegated_service_agent",
            _ => "unknown",
        };
}
