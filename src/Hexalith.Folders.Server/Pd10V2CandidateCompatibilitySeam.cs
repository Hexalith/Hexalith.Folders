using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server;

/// <summary>
/// Provides an isolated, opt-in transport seam for exercising the generated PD10 v2 client against
/// the historical in-process endpoint implementation. Production composition must not select this seam.
/// </summary>
public static class Pd10V2CandidateCompatibilitySeam
{
    private const string CandidatePrefix = "/api/v2";
    private const string HistoricalPrefix = "/api/v1";

    /// <summary>
    /// Rewrites candidate transport paths and request-schema discriminators before endpoint routing.
    /// This is intentionally separate from <c>MapFoldersServerEndpoints</c> so v2 remains non-routed
    /// unless an isolated candidate test host opts in explicitly.
    /// </summary>
    /// <param name="app">The isolated candidate application pipeline.</param>
    /// <returns>The supplied application builder.</returns>
    public static IApplicationBuilder UsePd10V2CandidateCompatibilitySeam(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            string path = context.Request.Path.Value ?? string.Empty;
            if (path.StartsWith(CandidatePrefix, StringComparison.Ordinal))
            {
                context.Request.Path = CandidatePathToHistoricalPath(path);
                await RewriteRequestSchemaVersionAsync(context.Request, context.RequestAborted).ConfigureAwait(false);
                await InvokeCandidateAsync(context, next).ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
        });
    }

    private static async Task InvokeCandidateAsync(HttpContext context, RequestDelegate next)
    {
        Stream responseBody = context.Response.Body;
        using MemoryStream capturedBody = new();
        context.Response.Body = capturedBody;
        try
        {
            await next(context).ConfigureAwait(false);
            capturedBody.Position = 0;
            if (context.Response.StatusCode is StatusCodes.Status403Forbidden or StatusCodes.Status404NotFound)
            {
                string correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? "correlation_absent";
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "application/problem+json";
                context.Response.ContentLength = null;
                await JsonSerializer.SerializeAsync(
                    responseBody,
                    new
                    {
                        type = "https://hexalith.dev/errors/folders/resource_unavailable",
                        title = "Resource not available.",
                        status = StatusCodes.Status404NotFound,
                        category = "tenant_access_denied",
                        code = "resource_unavailable",
                        message = "Access is denied. The caller is not authorized for this operation or resource.",
                        correlationId,
                        retryable = false,
                        clientAction = "no_action",
                        details = new { visibility = "redacted" },
                    },
                    cancellationToken: context.RequestAborted).ConfigureAwait(false);
                return;
            }

            context.Response.ContentLength = capturedBody.Length;
            await capturedBody.CopyToAsync(responseBody, context.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = responseBody;
        }
    }

    private static string CandidatePathToHistoricalPath(string path)
    {
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is ["api", "v2", "folders", _, "tasks", _, "status"])
        {
            return $"{HistoricalPrefix}/tasks/{segments[5]}/status";
        }

        if (segments is ["api", "v2", "folders", _, "ops-console", "readiness-diagnostics"])
        {
            return $"{HistoricalPrefix}/ops-console/readiness-diagnostics";
        }

        if (segments is ["api", "v2", "folders", _, "ops-console", "projection-freshness"])
        {
            return $"{HistoricalPrefix}/ops-console/projection-freshness";
        }

        return string.Concat(HistoricalPrefix, path.AsSpan(CandidatePrefix.Length));
    }

    private static async Task RewriteRequestSchemaVersionAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.Body == Stream.Null
            || request.ContentType is null
            || !request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using StreamReader reader = new(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        string candidateJson = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        string historicalJson = candidateJson
            .Replace("\"requestSchemaVersion\":\"v2\"", "\"requestSchemaVersion\":\"v1\"", StringComparison.Ordinal)
            .Replace("\"requestSchemaVersion\": \"v2\"", "\"requestSchemaVersion\": \"v1\"", StringComparison.Ordinal);
        byte[] bytes = Encoding.UTF8.GetBytes(historicalJson);
        request.Body = new MemoryStream(bytes, writable: false);
        request.ContentLength = bytes.Length;
    }
}
