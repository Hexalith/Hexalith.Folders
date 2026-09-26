using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Claims;

using Hexalith.Folders.Observability;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server;

/// <summary>Records bounded, metadata-only traffic for the public Folders API versions.</summary>
public static class FoldersApiRouteTelemetry
{
    /// <summary>The meter name used by route observations.</summary>
    public const string MeterName = FolderTelemetryNames.MeterName;

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("folders.api.requests");
    private static readonly Counter<long> Errors = Meter.CreateCounter<long>("folders.api.errors");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("folders.api.duration", "ms");

    /// <summary>Adds public API request attribution after authentication and before route dispatch.</summary>
    public static IApplicationBuilder UseFoldersApiRouteTelemetry(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(async (context, next) =>
        {
            string? version = Version(context.Request.Path);
            if (version is null)
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            string consumer = Consumer(context.User);
            long started = Stopwatch.GetTimestamp();
            try
            {
                await next(context).ConfigureAwait(false);
            }
            finally
            {
                int status = context.Response.StatusCode;
                TagList tags = new()
                {
                    { "api.version", version },
                    { "consumer", consumer },
                    { "http.status_code", status },
                };
                Requests.Add(1, tags);
                if (status >= StatusCodes.Status400BadRequest)
                {
                    Errors.Add(1, tags);
                }

                Duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tags);
                Activity.Current?.SetTag("folders.api.version", version);
                Activity.Current?.SetTag("folders.consumer", consumer);
                Activity.Current?.SetTag("folders.http.status_code", status);
            }
        });
    }

    private static string? Version(PathString path)
        => path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase) ? "v1"
            : path.StartsWithSegments("/api/v2", StringComparison.OrdinalIgnoreCase) ? "v2"
            : null;

    private static string Consumer(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return "unattributed";
        }

        string? clientId = principal.FindFirst("client_id")?.Value ?? principal.FindFirst("azp")?.Value;
        return string.Equals(clientId, "Hexalith.Projects", StringComparison.OrdinalIgnoreCase)
            ? "projects"
            : string.IsNullOrWhiteSpace(clientId) ? "unattributed" : "other";
    }
}
