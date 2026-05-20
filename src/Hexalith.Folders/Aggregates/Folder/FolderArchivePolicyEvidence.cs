namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderArchivePolicyEvidence(
    FolderArchivePolicyOutcome Outcome,
    string? ManagedTenantId,
    string? FolderId,
    string? PolicyVersion)
{
    public static FolderArchivePolicyEvidence Allowed(
        string managedTenantId,
        string folderId,
        string policyVersion)
        => new(FolderArchivePolicyOutcome.Allowed, managedTenantId, folderId, policyVersion);

    public static FolderArchivePolicyEvidence Denied(
        string managedTenantId,
        string folderId,
        string policyVersion)
        => new(FolderArchivePolicyOutcome.Denied, managedTenantId, folderId, policyVersion);
}
