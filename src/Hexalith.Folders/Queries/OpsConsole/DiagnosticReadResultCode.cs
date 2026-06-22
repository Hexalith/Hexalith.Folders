namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Outcome codes shared by the ops-console diagnostics query handlers. Maps to the canonical
/// read-op transport result classes (Story 8.2). Diagnostics map <see cref="ProjectionStale"/> to
/// HTTP 409 (not 503) per the spine, and <see cref="ProjectionUnavailable"/>/<see cref="ReadModelUnavailable"/> to 503.
/// </summary>
public enum DiagnosticReadResultCode
{
    /// <summary>Authorized; the diagnostic view is returned.</summary>
    Allowed,

    /// <summary>Authentication is required.</summary>
    AuthenticationRequired,

    /// <summary>Authorization denied (safe).</summary>
    AuthorizationDenied,

    /// <summary>No such diagnostic for the authoritative tenant (safe, indistinguishable from unauthorized).</summary>
    NotFoundSafe,

    /// <summary>Backing projection is stale beyond the safe threshold (HTTP 409 for diagnostics).</summary>
    ProjectionStale,

    /// <summary>Backing projection is unavailable.</summary>
    ProjectionUnavailable,

    /// <summary>The read model is unavailable.</summary>
    ReadModelUnavailable,
}
