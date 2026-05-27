namespace Hexalith.Folders.Queries.Folders;

public enum BranchRefPolicyQueryResultCode
{
    Allowed,
    AuthenticationRequired,
    AuthorizationDenied,
    NotFoundSafe,
    ProjectionStale,
    ProjectionUnavailable,
    ReadModelUnavailable,
}
