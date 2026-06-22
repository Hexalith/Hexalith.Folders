namespace Hexalith.Folders.Queries.ProviderReadiness;

/// <summary>
/// Outcome codes for <see cref="GetProviderBindingQueryHandler"/>.
/// </summary>
public enum GetProviderBindingQueryResultCode
{
    /// <summary>Authorized; the redacted binding metadata is returned.</summary>
    Allowed,

    /// <summary>Authentication is required.</summary>
    AuthenticationRequired,

    /// <summary>Authorization denied (externally indistinguishable safe denial).</summary>
    AuthorizationDenied,

    /// <summary>No such binding for the authoritative tenant (safe denial).</summary>
    NotFoundSafe,

    /// <summary>Authorization projection is stale.</summary>
    ProjectionStale,

    /// <summary>Authorization projection is unavailable.</summary>
    ProjectionUnavailable,

    /// <summary>The binding read model is unavailable.</summary>
    ReadModelUnavailable,
}
