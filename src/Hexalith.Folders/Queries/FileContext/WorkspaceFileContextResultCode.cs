namespace Hexalith.Folders.Queries.FileContext;

public enum WorkspaceFileContextResultCode
{
    Allowed,
    AuthenticationRequired,
    AuthorizationDenied,
    NotFoundSafe,
    ValidationFailed,
    PathValidationFailed,
    Redacted,
    InputLimitExceeded,
    ResponseLimitExceeded,
    QueryTimeout,
    ReadModelUnavailable,
    ProjectionStale,
    ProjectionUnavailable,
    RangeUnsatisfiable,
}
