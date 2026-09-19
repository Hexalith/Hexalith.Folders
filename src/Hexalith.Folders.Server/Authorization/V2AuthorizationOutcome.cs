namespace Hexalith.Folders.Server.Authorization;

/// <summary>Closed PD10 v2 pre-observation authorization outcome.</summary>
/// <param name="Allowed">Whether protected observation may execute.</param>
/// <param name="StatusCode">HTTP status code when denied.</param>
/// <param name="Category">Canonical error category when denied.</param>
/// <param name="Code">Canonical error code when denied.</param>
/// <param name="Retryable">Whether retry can help.</param>
/// <param name="ClientAction">Closed client-action token.</param>
/// <param name="Visibility">Closed visibility token.</param>
internal sealed record V2AuthorizationOutcome(
    bool Allowed,
    int? StatusCode,
    string? Category,
    string? Code,
    bool Retryable,
    string ClientAction,
    string Visibility)
{
    /// <summary>Creates the successful pre-observation outcome.</summary>
    public static V2AuthorizationOutcome Allow() => new(true, null, null, null, false, "no_action", "metadata_only");

    /// <summary>Creates the exact unauthenticated outcome.</summary>
    public static V2AuthorizationOutcome AuthenticationFailure() =>
        new(false, 401, "authentication_failure", "authentication_required", false, "check_credentials", "redacted");

    /// <summary>Creates the exact non-enumerating fresh-negative outcome.</summary>
    public static V2AuthorizationOutcome SafeDenial() =>
        new(false, 404, "tenant_access_denied", "resource_unavailable", false, "no_action", "redacted");

    /// <summary>Creates the exact retryable unusable-authority outcome.</summary>
    public static V2AuthorizationOutcome AuthorityUnavailable() =>
        new(false, 503, "read_model_unavailable", "projection_unavailable", true, "retry", "redacted");
}
