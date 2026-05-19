namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderAccessAclEvidence(
    FolderAccessAclOutcome Outcome,
    string? ManagedTenantId,
    string? OrganizationId,
    string? FolderId,
    string? PrincipalId,
    string Action)
{
    // Story 2.4 reuses an existing Story 2.2 organization ACL action as the management
    // proof; it does not create a new folder-level manage-access token.
    public const string ManagementAction = "configure_provider_binding";

    public static FolderAccessAclEvidence Allowed(
        string managedTenantId,
        string organizationId,
        string folderId,
        string principalId)
        => new(FolderAccessAclOutcome.Allowed, managedTenantId, organizationId, folderId, principalId, ManagementAction);
}
