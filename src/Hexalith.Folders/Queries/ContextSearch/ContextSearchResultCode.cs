namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// Canonical outcomes of an authorized semantic-index search over <c>folders-index</c>. The first member is
/// <see cref="Allowed"/> by convention; denied, absent, and cross-tenant targets all map to the same
/// externally-indistinguishable safe-denial shape at the transport boundary.
/// </summary>
public enum ContextSearchResultCode
{
    Allowed,
    AuthenticationRequired,
    AuthorizationDenied,
    NotFoundSafe,
    ValidationFailed,
    InputLimitExceeded,
    ResponseLimitExceeded,
    QueryTimeout,
    ReadModelUnavailable,
}
