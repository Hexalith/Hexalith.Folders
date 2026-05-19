namespace Hexalith.Folders.Aggregates.Folder;

public enum FolderAccessAclOutcome
{
    Allowed,
    Denied,
    Unavailable,
    Malformed,
    Stale,
    TenantMismatch,
    FolderMismatch,
    UnsupportedAction,
}
