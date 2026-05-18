namespace Hexalith.Folders.Aggregates.Organization;

public sealed record OrganizationAclOperation(
    OrganizationAclOperationIntent Intent,
    OrganizationAclPrincipalKind PrincipalKind,
    string PrincipalId,
    string Action);
