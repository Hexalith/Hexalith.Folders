using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Mcp.Credentials;

namespace Hexalith.Folders.Mcp.Composition;

/// <summary>
/// Attaches the resolved bearer token to every SDK request. This mirrors the Story 5.1/5.2
/// <c>BearerTokenHandler</c> pattern: authentication is owned by the caller (here, the MCP composition
/// root) and wired onto the <c>AddFoldersClient</c> HTTP pipeline rather than by the SDK module. The token
/// is held in memory only and is never written to any output channel (stdout is the JSON-RPC channel; logs
/// go to stderr and are metadata-only).
/// </summary>
/// <remarks>
/// The handler resolves the token through <see cref="McpCredentialResolver"/>. When no token resolves the
/// handler sends the request without an <c>Authorization</c> header; in practice the tool pipeline
/// short-circuits with <c>credential_missing</c> before any request reaches this handler, so a missing
/// token never produces a live unauthenticated call.
/// </remarks>
internal sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly McpCredentialResolver _credentials;

    /// <summary>Initializes a new instance of the <see cref="BearerTokenHandler"/> class.</summary>
    /// <param name="credentials">The credential resolver supplying the bearer token.</param>
    public BearerTokenHandler(McpCredentialResolver credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        _credentials = credentials;
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? token = _credentials.ResolveToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
