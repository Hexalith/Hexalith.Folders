namespace Hexalith.Folders.Queries.FileContext;

public enum WorkspaceFileContextResultCode
{
    PolicyUnavailable,
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
