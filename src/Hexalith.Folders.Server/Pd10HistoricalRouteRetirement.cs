using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.Folders.Server;

/// <summary>Retires the external historical v1 API while the PD10 v2 seam keeps its internal v1 dispatch.</summary>
public static class Pd10HistoricalRouteRetirement
{
    private const string HistoricalRoutePrefix = "api/v1";

    /// <summary>
    /// Answers every external v1 request with the canonical 404 before authorization, lookup, or effect.
    /// Compose after <c>UseRouting</c> and <c>UsePd10V2CandidateCompatibilitySeam</c>, and before
    /// <c>UseAuthorization</c>.
    /// </summary>
    /// <param name="app">The application pipeline.</param>
    /// <returns>The same pipeline.</returns>
    public static IApplicationBuilder UsePd10HistoricalRouteRetirement(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(async (context, next) =>
        {
            if (IsHistoricalRequest(context) && !Pd10V2CandidateCompatibilitySeam.IsHistoricalDispatch(context))
            {
                await Pd10V2CandidateCompatibilitySeam.WriteRetiredHistoricalRouteAsync(context).ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
        });
    }

    // Routing matches case-insensitively, so compare both the request path and the selected endpoint's
    // pattern case-insensitively.
    private static bool IsHistoricalRequest(HttpContext context)
        => context.Request.Path.StartsWithSegments("/" + HistoricalRoutePrefix, StringComparison.OrdinalIgnoreCase)
            || (context.GetEndpoint() is RouteEndpoint { RoutePattern.RawText: { } pattern }
                && IsHistoricalPattern(pattern.TrimStart('/')));

    private static bool IsHistoricalPattern(string pattern)
        => pattern.Equals(HistoricalRoutePrefix, StringComparison.OrdinalIgnoreCase)
            || pattern.StartsWith(HistoricalRoutePrefix + "/", StringComparison.OrdinalIgnoreCase);
}
