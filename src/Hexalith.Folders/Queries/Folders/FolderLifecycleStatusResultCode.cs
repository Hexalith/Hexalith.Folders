namespace Hexalith.Folders.Queries.Folders;

public enum FolderLifecycleStatusResultCode
{
    Allowed,
    AuthenticationRequired,
    AuthorizationDenied,
    NotFoundSafe,
    ProjectionStale,
    ReadModelUnavailable,
}
