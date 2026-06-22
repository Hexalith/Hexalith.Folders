namespace Hexalith.Folders.Queries.FolderAccess;

/// <summary>
/// Outcome codes for <see cref="ListFolderAclEntriesQueryHandler"/>.
/// </summary>
public enum ListFolderAclEntriesQueryResultCode
{
    /// <summary>Authorized; entries are returned.</summary>
    Allowed,

    /// <summary>Authentication is required.</summary>
    AuthenticationRequired,

    /// <summary>Authorization denied (safe).</summary>
    AuthorizationDenied,

    /// <summary>No such folder for the authoritative tenant (safe).</summary>
    NotFoundSafe,

    /// <summary>Authorization projection stale.</summary>
    ProjectionStale,

    /// <summary>Authorization projection unavailable.</summary>
    ProjectionUnavailable,

    /// <summary>The read model is unavailable.</summary>
    ReadModelUnavailable,
}
