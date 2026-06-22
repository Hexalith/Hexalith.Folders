namespace Hexalith.Folders.Queries.Folders;

/// <summary>
/// Outcome codes for <see cref="WorkspaceTransitionEvidenceQueryHandler"/>.
/// </summary>
public enum WorkspaceTransitionEvidenceQueryResultCode
{
    /// <summary>Authorized; the evidence is returned.</summary>
    Allowed,

    /// <summary>Authentication is required.</summary>
    AuthenticationRequired,

    /// <summary>Authorization denied (safe).</summary>
    AuthorizationDenied,

    /// <summary>No such workspace for the authoritative tenant (safe).</summary>
    NotFoundSafe,

    /// <summary>Authorization projection stale.</summary>
    ProjectionStale,

    /// <summary>Authorization projection unavailable.</summary>
    ProjectionUnavailable,

    /// <summary>The read model is unavailable.</summary>
    ReadModelUnavailable,
}
