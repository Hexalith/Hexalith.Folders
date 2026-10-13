using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.Authorization;

/// <summary>A current organization ACL grant for one principal and exact action token.</summary>
/// <param name="PrincipalKind">The kind of principal receiving the grant.</param>
/// <param name="PrincipalId">The principal identifier.</param>
/// <param name="Action">The granted action token.</param>
public sealed record OrganizationPermissionGrant(OrganizationAclPrincipalKind PrincipalKind,
    string PrincipalId, string Action);
