namespace Hexalith.Folders.Observability;

public enum FolderAuditResult
{
    Unknown = 0,
    Success = 1,
    Denied = 2,
    Failed = 3,
    Rejected = 4,
    Duplicate = 5,
    Retried = 6,
    Replayed = 7,
    Locked = 8,
    Stale = 9,
    Unavailable = 10,
}
