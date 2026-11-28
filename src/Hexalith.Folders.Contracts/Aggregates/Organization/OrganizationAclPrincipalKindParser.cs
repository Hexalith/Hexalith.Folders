namespace Hexalith.Folders.Aggregates.Organization;

/// <summary>Parses the closed organization ACL principal tokens at command admission and dispatch.</summary>
public static class OrganizationAclPrincipalKindParser
{
    /// <summary>Accepts named principal kinds and the delegated-service wire token, excluding numeric enum values.</summary>
    /// <param name="value">The principal kind supplied by the command payload.</param>
    /// <param name="kind">The parsed kind when the token is recognized.</param>
    /// <returns>Whether the token identifies a supported principal kind.</returns>
    public static bool TryParse(string? value, out OrganizationAclPrincipalKind kind)
    {
        kind = value?.ToLowerInvariant() switch
        {
            "user" => OrganizationAclPrincipalKind.User,
            "group" => OrganizationAclPrincipalKind.Group,
            "role" => OrganizationAclPrincipalKind.Role,
            "delegated_service_agent" or "delegatedserviceagent" => OrganizationAclPrincipalKind.DelegatedServiceAgent,
            _ => (OrganizationAclPrincipalKind)(-1),
        };
        return Enum.IsDefined(kind);
    }
}
