using System.Collections.Generic;

using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Mcp.Errors;

/// <summary>
/// The canonical metadata-only failure result returned by every Folders MCP tool and resource. Carries the
/// authoritative failure <see cref="Kind"/> plus the cross-adapter parity fields
/// (<see cref="CorrelationId"/>, <see cref="Code"/>, <see cref="Retryable"/>, <see cref="ClientAction"/>).
/// </summary>
/// <remarks>
/// Serialized camelCase via <c>MetadataOnlyJson</c>; the optional <see cref="Message"/>/<see cref="Details"/>
/// come from the typed <see cref="ProblemDetails"/> (themselves metadata-only by spine contract) for post-SDK
/// failures, or locally-known constants for the pre-SDK kinds. The correlation ID used by the call is always
/// present (success and failure) for caller correlation.
/// </remarks>
internal sealed record McpFailure
{
    /// <summary>Gets the authoritative failure kind (canonical category name, or a pre-SDK kind).</summary>
    public required string Kind { get; init; }

    /// <summary>Gets the correlation ID used by the call (always present).</summary>
    public required string CorrelationId { get; init; }

    /// <summary>Gets the canonical error code, when known.</summary>
    public string? Code { get; init; }

    /// <summary>Gets a value indicating whether the operation is retryable as reported by the server (false for pre-SDK failures).</summary>
    public bool Retryable { get; init; }

    /// <summary>Gets the recommended client action token.</summary>
    public string? ClientAction { get; init; }

    /// <summary>Gets an optional metadata-only message.</summary>
    public string? Message { get; init; }

    /// <summary>Gets optional metadata-only detail key/value pairs (post-SDK only).</summary>
    public IReadOnlyDictionary<string, string>? Details { get; init; }

    /// <summary>
    /// Builds the pre-SDK <c>usage_error</c> failure (missing required field or invalid configuration). No
    /// HTTP call has been made; the canonical category is <c>client_configuration_error</c>.
    /// </summary>
    /// <param name="correlationId">The correlation ID resolved for the call.</param>
    /// <param name="message">A metadata-only message that never echoes caller content.</param>
    /// <returns>The usage-error failure.</returns>
    public static McpFailure UsageError(string correlationId, string message) => new()
    {
        Kind = FailureKindProjection.UsageError,
        CorrelationId = correlationId,
        Code = "client_configuration_error",
        Retryable = false,
        ClientAction = "revise_request",
        Message = message,
    };

    /// <summary>
    /// Builds the pre-SDK <c>credential_missing</c> failure. No HTTP call has been made; no token, path, or
    /// auth section text is included.
    /// </summary>
    /// <param name="correlationId">The correlation ID resolved for the call.</param>
    /// <returns>The credential-missing failure.</returns>
    public static McpFailure Credentials(string correlationId) => new()
    {
        Kind = FailureKindProjection.CredentialMissing,
        CorrelationId = correlationId,
        Code = "credential_missing",
        Retryable = false,
        ClientAction = "check_credentials",
        Message = "No bearer token resolved from server configuration (auth.token / auth.tokenFile / HEXALITH_TOKEN).",
    };

    /// <summary>
    /// Builds a post-SDK failure by projecting the typed <see cref="ProblemDetails"/> category to a kind and
    /// carrying the parity fields verbatim.
    /// </summary>
    /// <param name="problem">The typed problem returned by the SDK.</param>
    /// <param name="correlationId">The correlation ID used by the call (echoed even if the body omits it).</param>
    /// <returns>The projected failure.</returns>
    public static McpFailure FromProblem(ProblemDetails problem, string correlationId) => new()
    {
        Kind = FailureKindProjection.Project(problem.Category),
        CorrelationId = string.IsNullOrWhiteSpace(problem.CorrelationId) ? correlationId : problem.CorrelationId,
        Code = problem.Code,
        Retryable = problem.Retryable,
        ClientAction = FailureKindProjection.ClientAction(problem.ClientAction),
        Message = problem.Message,
        Details = problem.Details is { Count: > 0 } ? problem.Details : null,
    };

    /// <summary>
    /// Builds the <c>internal_error</c> failure for a bare/untyped API exception (unexpected or unmapped
    /// status, null typed body). The correlation ID is always present.
    /// </summary>
    /// <param name="correlationId">The correlation ID used by the call.</param>
    /// <returns>The internal-error failure.</returns>
    public static McpFailure Internal(string correlationId) => new()
    {
        Kind = FailureKindProjection.InternalError,
        CorrelationId = correlationId,
        Code = "internal_error",
        Retryable = false,
        ClientAction = "contact_operator",
        Message = "An unexpected server response was received.",
    };

    /// <summary>
    /// Builds the content-safe <c>input_limit_exceeded</c> failure for inline content that exceeds the
    /// upload boundary. The message never discloses the byte limit or any content.
    /// </summary>
    /// <param name="correlationId">The correlation ID used by the call.</param>
    /// <returns>The input-limit failure.</returns>
    public static McpFailure InputLimitExceeded(string correlationId) => new()
    {
        Kind = "input_limit_exceeded",
        CorrelationId = correlationId,
        Code = "input_limit_exceeded",
        Retryable = false,
        ClientAction = "revise_request",
        Message = "File content exceeds the inline upload limit; stage it out of band and retry via the streamed transport.",
    };
}
