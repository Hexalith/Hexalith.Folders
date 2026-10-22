namespace Hexalith.Folders.Queries.FileContext;

public enum WorkspaceFileContextSourceStatus
{
    PolicyUnavailable,
    NotFoundSafe,
    ValidationFailed,
    PathValidationFailed,
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
