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

    // Fail-loud on undefined enum values is intentional: a folder event stream that
    // produces an unknown PrincipalKind reflects either a writer-side bug, deliberate
    // tampering, or unsupported schema drift. Per the project's fail-closed rule we
    // do not tolerate unknown principal kinds — silently bucketing them under an
    // opaque token would let a poisoned stream advance state past the gate without
    // a corresponding authorization model. Schema evolution must add new enum
    // members and the matching token in the same change.
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
