namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// Outcomes of the folder/tenant-scoped indexing-status projection read. Denied/absent/cross-tenant targets
/// collapse to the same safe-denial shape; the first member is <see cref="Allowed"/> by convention.
/// </summary>
public enum FolderIndexingStatusResultCode
{
    Allowed,
    AuthenticationRequired,
    AuthorizationDenied,
    NotFoundSafe,
    ReadModelUnavailable,
}
