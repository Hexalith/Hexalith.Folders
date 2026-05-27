using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Hexalith.Folders.Cli.Composition;

/// <summary>
/// Attaches the resolved bearer token to every SDK request. This mirrors the Story 5.1 sample
/// <c>BearerTokenHandler</c> pattern: authentication is owned by the caller (here, the CLI composition
/// root) and wired onto the <c>AddFoldersClient</c> HTTP pipeline rather than by the SDK module. The token
/// is held in memory only and is never written to any output channel.
/// </summary>
internal sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly string _token;

    /// <summary>Initializes a new instance of the <see cref="BearerTokenHandler"/> class.</summary>
    /// <param name="token">The resolved, non-blank bearer token.</param>
    public BearerTokenHandler(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _token = token;
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return base.SendAsync(request, cancellationToken);
    }
}
