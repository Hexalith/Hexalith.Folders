using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace Hexalith.Folders.IntegrationTests;

/// <summary>
/// Creates a test-only transport adapter that lets historical v1 runtime integration scenarios exercise
/// the generated v2 candidate client without exposing v2 routes from the production server.
/// </summary>
internal static class V2CandidateTestClient
{
    public static HttpClient Create(WebApplication app) => new(new RouteRewriteHandler(app.GetTestServer().CreateHandler()))
    {
        BaseAddress = new Uri("http://localhost"),
    };

    private sealed class RouteRewriteHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri uri = request.RequestUri ?? throw new InvalidOperationException("Candidate test request URI is missing.");
            string pathAndQuery = uri.PathAndQuery;
            if (pathAndQuery.StartsWith("/api/v2/", StringComparison.Ordinal))
            {
                UriBuilder builder = new(uri)
                {
                    Path = "/api/v1/" + uri.AbsolutePath["/api/v2/".Length..],
                    Query = uri.Query.TrimStart('?'),
                };
                request.RequestUri = builder.Uri;
            }

            HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.NotFound || response.Content is null)
            {
                return response;
            }

            string legacyBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string correlationId = RequestCorrelation(request);
            if (!string.IsNullOrWhiteSpace(legacyBody))
            {
                using JsonDocument document = JsonDocument.Parse(legacyBody);
                if (document.RootElement.TryGetProperty("correlationId", out JsonElement correlation)
                    && correlation.GetString() is { Length: > 0 } responseCorrelation)
                {
                    correlationId = responseCorrelation;
                }
            }

            string v2Body = JsonSerializer.Serialize(new
            {
                type = "about:blank",
                title = "Access unavailable",
                status = 404,
                category = "tenant_access_denied",
                code = "resource_unavailable",
                message = "The requested resource is unavailable.",
                correlationId,
                retryable = false,
                clientAction = "no_action",
                details = new { visibility = "redacted" },
            });
            response.Content = new StringContent(v2Body, Encoding.UTF8, "application/problem+json");
            return response;
        }

        private static string RequestCorrelation(HttpRequestMessage request) =>
            request.Headers.TryGetValues("X-Correlation-Id", out IEnumerable<string>? values)
                ? values.FirstOrDefault() ?? "opaque-correlation"
                : "opaque-correlation";
    }
}
