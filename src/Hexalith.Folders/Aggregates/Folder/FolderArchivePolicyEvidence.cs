namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderArchivePolicyEvidence(
    FolderArchivePolicyOutcome Outcome,
    string? ManagedTenantId,
    string? OrganizationId,
    string? FolderId,
    string? PolicyVersion)
{
    public static FolderArchivePolicyEvidence Allowed(
        string managedTenantId,
        string organizationId,
        string folderId,
        string policyVersion)
        => new(FolderArchivePolicyOutcome.Allowed, managedTenantId, organizationId, folderId, policyVersion);

    public static FolderArchivePolicyEvidence Denied(
        string managedTenantId,
        string organizationId,
        string folderId,
        string policyVersion)
        => new(FolderArchivePolicyOutcome.Denied, managedTenantId, organizationId, folderId, policyVersion);
}
