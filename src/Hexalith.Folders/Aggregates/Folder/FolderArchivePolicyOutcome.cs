namespace Hexalith.Folders.Aggregates.Folder;

public enum FolderArchivePolicyOutcome
{
    Allowed,
    Denied,
    Unavailable,
    Malformed,
    Stale,
    ScopeMismatch,
}
