using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Hexalith.Folders.Server;

/// <summary>
/// Reads the first non-empty, control-character-free header or query value from an HTTP request.
/// Unsafe values are skipped so they cannot be echoed into response headers or ProblemDetails.
/// </summary>
internal static class FolderHttpHeaderReader
{
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
