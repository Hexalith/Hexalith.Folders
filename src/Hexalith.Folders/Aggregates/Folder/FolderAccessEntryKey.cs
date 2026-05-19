namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderAccessEntryKey(
    string ManagedTenantId,
    string FolderId,
    FolderAccessPrincipalKind PrincipalKind,
    string PrincipalId,
    string Action)
{
    public string CanonicalValue
        => string.Join(
            "|",
            ManagedTenantId,
            FolderId,
            PrincipalKindToken,
            PrincipalId,
            Action);

    public string PrincipalKindToken
        => PrincipalKind switch
        {
            FolderAccessPrincipalKind.User => "user",
            FolderAccessPrincipalKind.Group => "group",
            FolderAccessPrincipalKind.Role => "role",
            FolderAccessPrincipalKind.DelegatedServiceAgent => "delegated_service_agent",
            _ => throw new InvalidOperationException($"Undefined FolderAccessPrincipalKind value: {(int)PrincipalKind}."),
        };
}
