namespace Hexalith.Folders.Aggregates.Folder;

public enum FolderArchiveAclOutcome
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
