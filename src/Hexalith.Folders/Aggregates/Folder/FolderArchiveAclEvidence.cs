namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderArchiveAclEvidence
{
    public const string ArchiveAction = "archive_folder";

    public FolderArchiveAclEvidence(
        FolderArchiveAclOutcome outcome,
        string? managedTenantId,
        string? organizationId,
        string? folderId,
        string? principalId,
        string action)
    {
        if (outcome == FolderArchiveAclOutcome.Allowed
            && !string.Equals(action, ArchiveAction, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Allowed FolderArchiveAclEvidence must carry the archive action '{ArchiveAction}'.",
                nameof(action));
        }

        Outcome = outcome;
        ManagedTenantId = managedTenantId;
        OrganizationId = organizationId;
        FolderId = folderId;
        PrincipalId = principalId;
        Action = action;
    }

    public FolderArchiveAclOutcome Outcome { get; init; }

    public string? ManagedTenantId { get; init; }

    public string? OrganizationId { get; init; }

    public string? FolderId { get; init; }

    public string? PrincipalId { get; init; }

    public string Action { get; init; }

    public static FolderArchiveAclEvidence Allowed(
        string managedTenantId,
        string organizationId,
        string folderId,
        string principalId)
        => new(FolderArchiveAclOutcome.Allowed, managedTenantId, organizationId, folderId, principalId, ArchiveAction);

    public static FolderArchiveAclEvidence Denied(
        string? managedTenantId,
        string? organizationId,
        string? folderId,
        string? principalId)
        => new(FolderArchiveAclOutcome.Denied, managedTenantId, organizationId, folderId, principalId, ArchiveAction);
}
