namespace Hexalith.Folders.Queries.FileContext;

public enum WorkspaceFileContextSourceStatus
{
    Available,
    Unavailable,
    Stale,
    Timeout,
    InputLimitExceeded,
    ResponseLimitExceeded,
    Redacted,
    BinaryDisallowed,
    LargeFileDisallowed,
    RangeUnsatisfiable,
}
