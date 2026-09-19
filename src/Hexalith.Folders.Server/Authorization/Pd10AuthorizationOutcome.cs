namespace Hexalith.Folders.Server.Authorization;

/// <summary>
/// Represents one closed PD10 pre-lookup authorization outcome.
/// </summary>
/// <param name="IsAllowed">Whether protected observation may proceed.</param>
/// <param name="StatusCode">HTTP status for a denied outcome, or 200 for an allowed result.</param>
/// <param name="Category">Canonical error category, or <c>success</c>.</param>
/// <param name="Code">Canonical error code, or <c>success</c>.</param>
/// <param name="Retryable">Whether retrying after authority recovery may help.</param>
/// <param name="ClientAction">Closed client-action token.</param>
/// <param name="Visibility">Closed visibility token.</param>
internal sealed record Pd10AuthorizationOutcome(
    bool IsAllowed,
    int StatusCode,
    string Category,
    string Code,
    bool Retryable,
    string ClientAction,
    string Visibility)
{
    /// <summary>Gets the allowed outcome.</summary>
    public static Pd10AuthorizationOutcome Allowed { get; } = new(
        true,
        StatusCodes.Status200OK,
        "success",
        "success",
        false,
        "no_action",
        "metadata_only");

    /// <summary>Gets the canonical unauthenticated outcome.</summary>
    public static Pd10AuthorizationOutcome AuthenticationRequired { get; } = new(
        false,
        StatusCodes.Status401Unauthorized,
        "authentication_failure",
        "authentication_required",
        false,
        "check_credentials",
        "redacted");

    /// <summary>Gets the byte-equivalent canonical fresh-negative outcome.</summary>
    public static Pd10AuthorizationOutcome SafeDenial { get; } = new(
        false,
        StatusCodes.Status404NotFound,
        "tenant_access_denied",
        "resource_unavailable",
        false,
        "no_action",
        "redacted");

    /// <summary>Gets the retryable non-disclosing unusable-authority outcome.</summary>
    public static Pd10AuthorizationOutcome AuthorityUnavailable { get; } = new(
        false,
        StatusCodes.Status503ServiceUnavailable,
        "read_model_unavailable",
        "projection_unavailable",
        true,
        "retry",
        "redacted");
}
