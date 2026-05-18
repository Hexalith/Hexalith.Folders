namespace Hexalith.Folders.Aggregates.Folder;

public enum FolderCreateAclOutcome
{
    Allowed,
    Denied,
    Unavailable,
    Malformed,
    Stale,
}
