namespace Hexalith.Folders.Queries.Folders;

public enum WorkspaceLockStatusQueryResultCode
{
    Allowed,
    AuthenticationRequired,
    NotFoundSafe,
    ProjectionStale,
    ProjectionUnavailable,
    ReadModelUnavailable,
    AuthorizationDenied,
}
