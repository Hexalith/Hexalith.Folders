using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Composition;
using Hexalith.Folders.Mcp.Configuration;
using Hexalith.Folders.Mcp.Credentials;
using Hexalith.Folders.Mcp.Tooling;

using Newtonsoft.Json.Linq;

using GeneratedClient = Hexalith.Folders.Client.Generated.Client;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Hermetic test helpers: builds credential resolvers, tool pipelines over a fake <see cref="IClient"/> or a
/// real generated client over a fake <see cref="HttpMessageHandler"/>, and parses tool-result JSON. No live
/// server, Dapr, Keycloak, Redis, or network is involved.
/// </summary>
internal static class TestSupport
{
    /// <summary>A non-secret token used to assert it never leaks into any output channel.</summary>
    public const string Token = "test-secret-token-value";

    /// <summary>Builds a credential resolver that resolves the given inline token (no env, no file access).</summary>
    /// <param name="token">The inline token, or <see langword="null"/> for the missing-credential case.</param>
    /// <returns>A hermetic credential resolver.</returns>
    public static McpCredentialResolver Resolver(string? token)
        => new(new FoldersMcpAuthOptions { Token = token }, environment: _ => null, fileReader: _ => null);

    /// <summary>Builds a tool pipeline over the supplied client with a resolved (or missing) token.</summary>
    /// <param name="client">The SDK client (a substitute or a real generated client over a fake handler).</param>
    /// <param name="token">The token to resolve, or <see langword="null"/> for the missing-credential case.</param>
    /// <returns>A tool pipeline.</returns>
    public static ToolPipeline Pipeline(IClient client, string? token = Token)
        => new(client, Resolver(token));

    /// <summary>Builds a real generated client over a capturing handler with a test base address.</summary>
    /// <param name="handler">The capturing handler.</param>
    /// <returns>A real <see cref="IClient"/>.</returns>
    public static IClient RealClient(CapturingHandler handler)
        => new GeneratedClient(new HttpClient(handler) { BaseAddress = new Uri("https://folders.test/") });

    /// <summary>
    /// Builds a real generated client whose HTTP pipeline runs the production <see cref="BearerTokenHandler"/>
    /// in front of the capturing handler — exactly as the composition root wires it — so a test can assert the
    /// resolved token is attached to the wire <c>Authorization</c> header (or absent when no token resolves).
    /// </summary>
    /// <param name="handler">The capturing handler that records the outgoing request.</param>
    /// <param name="token">The token the resolver should resolve, or <see langword="null"/> for the no-token case.</param>
    /// <returns>A real <see cref="IClient"/> with the bearer handler in the pipeline.</returns>
    public static IClient BearerClient(CapturingHandler handler, string? token = Token)
    {
        BearerTokenHandler bearer = new(Resolver(token)) { InnerHandler = handler };
        return new GeneratedClient(new HttpClient(bearer) { BaseAddress = new Uri("https://folders.test/") });
    }

    /// <summary>Parses a tool-result JSON string into a navigable object.</summary>
    /// <param name="json">The tool result.</param>
    /// <returns>The parsed token.</returns>
    public static JObject Parse(string json) => JObject.Parse(json);

    /// <summary>Reads the <c>kind</c> property of a failure result, or <see langword="null"/> for a success envelope.</summary>
    /// <param name="json">The tool result JSON.</param>
    /// <returns>The failure kind, or <see langword="null"/>.</returns>
    public static string? Kind(string json) => Parse(json).Value<string>("kind");

    /// <summary>Reads the top-level <c>correlationId</c> property of any tool result.</summary>
    /// <param name="json">The tool result JSON.</param>
    /// <returns>The correlation ID.</returns>
    public static string? CorrelationId(string json) => Parse(json).Value<string>("correlationId");

    /// <summary>A fake <see cref="HttpMessageHandler"/> that records requests and returns a canned response.</summary>
    public sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly string _contentType;

        /// <summary>Initializes a new instance of the <see cref="CapturingHandler"/> class.</summary>
        /// <param name="status">The canned response status.</param>
        /// <param name="body">The canned response body.</param>
        /// <param name="contentType">The canned response content type.</param>
        public CapturingHandler(HttpStatusCode status, string body, string contentType = "application/json")
        {
            _status = status;
            _body = body;
            _contentType = contentType;
        }

        /// <summary>Gets the captured requests in order.</summary>
        public List<CapturedRequest> Requests { get; } = [];

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add(new CapturedRequest(
                request.Method.Method,
                request.RequestUri,
                request.Headers.TryGetValues("X-Correlation-Id", out IEnumerable<string>? c) ? c.FirstOrDefault() : null,
                request.Headers.TryGetValues("X-Hexalith-Task-Id", out IEnumerable<string>? t) ? t.FirstOrDefault() : null,
                request.Headers.Authorization?.ToString(),
                body,
                request.Headers.TryGetValues("X-Hexalith-Freshness", out IEnumerable<string>? f) ? f.FirstOrDefault() : null,
                request.Headers.TryGetValues("Idempotency-Key", out IEnumerable<string>? i) ? i.FirstOrDefault() : null));

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, _contentType),
            };
        }
    }

    /// <summary>A captured request's metadata-only observable fields.</summary>
    /// <param name="Method">The HTTP method.</param>
    /// <param name="Uri">The request URI.</param>
    /// <param name="CorrelationId">The <c>X-Correlation-Id</c> header value.</param>
    /// <param name="TaskId">The <c>X-Hexalith-Task-Id</c> header value.</param>
    /// <param name="Authorization">The <c>Authorization</c> header value.</param>
    /// <param name="Body">The request body.</param>
    /// <param name="Freshness">The <c>X-Hexalith-Freshness</c> header value, or <see langword="null"/> when not sent.</param>
    /// <param name="IdempotencyKey">The <c>Idempotency-Key</c> header value, or <see langword="null"/> when not sent.</param>
    public sealed record CapturedRequest(string Method, Uri? Uri, string? CorrelationId, string? TaskId, string? Authorization, string? Body, string? Freshness, string? IdempotencyKey);
}
