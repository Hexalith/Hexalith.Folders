namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderCreateAclEvidence(
    FolderCreateAclOutcome Outcome,
    string? ManagedTenantId,
    string? OrganizationId,
    string? PrincipalId,
    string Action)
{
    /// <summary>Gets the action bound to this ACL evidence.</summary>
    public string Action
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(Action));
    } = Action ?? throw new ArgumentNullException(nameof(Action));

    public static FolderCreateAclEvidence Allowed(
        string managedTenantId,
        string organizationId,
        string principalId)
        => new(FolderCreateAclOutcome.Allowed, managedTenantId, organizationId, principalId, "create_folder");
}
