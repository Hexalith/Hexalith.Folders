namespace Hexalith.Folders.Authorization;

public enum FolderPermissionEvidenceStatus
{
    Allowed,
    Denied,
    NotFoundSafe,
    Stale,
    Unavailable,
    Malformed,
}
