using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Hexalith.Folders.Server;

/// <summary>
/// Reads the first non-empty, control-character-free header or query value from an HTTP request.
/// Unsafe values are skipped so they cannot be echoed into response headers or ProblemDetails.
/// </summary>
internal static class FolderHttpHeaderReader
{
    /// <summary>Returns whether a read operation must reject the idempotency header.</summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="includeEmpty">Whether the route rejects even an empty or unusable header.</param>
    /// <returns>Whether the route's configured header check found an idempotency key.</returns>
    public static bool HasReadIdempotencyKey(HttpContext httpContext, bool includeEmpty = false)
        => includeEmpty
            ? httpContext.Request.Headers.ContainsKey("Idempotency-Key")
            : ReadHeader(httpContext, "Idempotency-Key") is not null;

    /// <summary>Returns whether the request supplies a freshness class other than the route's accepted class.</summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="expectedFreshness">The route's accepted freshness class.</param>
    /// <returns>Whether the first usable freshness header differs from the accepted class.</returns>
    public static bool HasUnsupportedReadFreshness(HttpContext httpContext, string expectedFreshness)
        => ReadHeader(httpContext, "X-Hexalith-Freshness") is { } requestedFreshness
            && !string.Equals(requestedFreshness, expectedFreshness, StringComparison.Ordinal);

    /// <summary>Validates any correlation or task identifiers supplied for a canonical-segment read.</summary>
    /// <param name="correlationId">The optional correlation identifier.</param>
    /// <param name="taskId">The optional task identifier.</param>
    /// <returns>Whether both identifiers are absent or canonical.</returns>
    public static bool AreValidReadHeaderIdentifiers(string? correlationId, string? taskId)
        => (correlationId is null || FolderCanonicalSegmentIdentifier.IsValid(correlationId))
            && (taskId is null || FolderCanonicalSegmentIdentifier.IsValid(taskId));

    /// <summary>Reads the optional diagnostic correlation identifier, declining an unsafe first nonblank value.</summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="correlationId">The trimmed identifier when present and safe.</param>
    /// <returns>Whether the header is absent, blank, or safe to echo.</returns>
    public static bool TryReadDiagnosticCorrelation(HttpContext httpContext, out string? correlationId)
    {
        correlationId = null;
        foreach (string? raw in httpContext.Request.Headers["X-Correlation-Id"])
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string candidate = raw.Trim();
            if (!IsSafeHeaderValue(candidate) || !FolderCanonicalPathIdentifier.IsSafeDiagnosticId(candidate))
            {
                return false;
            }

            correlationId = candidate;
            return true;
        }

        return true;
    }

    /// <summary>
    /// Returns the first non-empty safe value for <paramref name="name"/> from the request headers.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="name">The header name.</param>
    /// <returns>The trimmed value, or <see langword="null"/> when none is safe.</returns>
    public static string? ReadHeader(HttpContext httpContext, string name)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return FirstNonEmpty(httpContext.Request.Headers.TryGetValue(name, out StringValues values) ? values : StringValues.Empty);
    }

    /// <summary>
    /// Returns the first non-empty safe value for <paramref name="name"/> from the request query string.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="name">The query parameter name.</param>
    /// <returns>The trimmed value, or <see langword="null"/> when none is safe.</returns>
    public static string? ReadQuery(HttpContext httpContext, string name)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return FirstNonEmpty(httpContext.Request.Query.TryGetValue(name, out StringValues values) ? values : StringValues.Empty);
    }

    /// <summary>
    /// Returns whether <paramref name="value"/> is safe to echo into an HTTP header.
    /// </summary>
    /// <param name="value">The candidate value.</param>
    /// <returns><see langword="true"/> when the value contains no CR, LF, or other control characters.</returns>
    public static bool IsSafeHeaderValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        foreach (char c in value)
        {
            if (c == '\r' || c == '\n' || char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    private static string? FirstNonEmpty(StringValues values)
    {
        foreach (string? raw in values)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string trimmed = raw.Trim();
            if (trimmed.Length == 0 || !IsSafeHeaderValue(trimmed))
            {
                continue;
            }

            return trimmed;
        }

        return null;
    }
}
