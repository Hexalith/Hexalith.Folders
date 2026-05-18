namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderCreateAclEvidence(
    FolderCreateAclOutcome Outcome,
    string? ManagedTenantId,
    string? OrganizationId,
    string? PrincipalId,
    string Action)
{
    public static FolderCreateAclEvidence Allowed(
        string managedTenantId,
        string organizationId,
        string principalId)
        => new(FolderCreateAclOutcome.Allowed, managedTenantId, organizationId, principalId, "create_folder");
}
