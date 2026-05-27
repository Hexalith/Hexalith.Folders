namespace Hexalith.Folders.Queries.Folders;

public enum WorkspaceStatusQueryResultCode
{
    Allowed,
    AuthenticationRequired,
    NotFoundSafe,
    ProjectionStale,
    ProjectionUnavailable,
    ReadModelUnavailable,
    AuthorizationDenied,
}
