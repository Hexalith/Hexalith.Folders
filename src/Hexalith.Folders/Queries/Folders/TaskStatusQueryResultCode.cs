namespace Hexalith.Folders.Queries.Folders;

public enum TaskStatusQueryResultCode
{
    Allowed,
    AuthenticationRequired,
    AuthorizationDenied,
    NotFoundSafe,
    ProjectionStale,
    ProjectionUnavailable,
    ReadModelUnavailable,
}
