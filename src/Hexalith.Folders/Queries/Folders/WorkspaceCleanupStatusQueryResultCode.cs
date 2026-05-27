namespace Hexalith.Folders.Queries.Folders;

public enum WorkspaceCleanupStatusQueryResultCode
{
    Allowed,
    AuthenticationRequired,
    NotFoundSafe,
    ProjectionStale,
    ProjectionUnavailable,
    ReadModelUnavailable,
    AuthorizationDenied,
}
