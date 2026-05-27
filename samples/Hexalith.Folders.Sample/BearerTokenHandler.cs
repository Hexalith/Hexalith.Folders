using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Hexalith.Folders.Sample;

/// <summary>
/// Placeholder bearer-token <see cref="DelegatingHandler"/> showing where callers attach authentication to
/// the Folders SDK. Authentication is intentionally outside the SDK module: this handler is wired onto the
/// <see cref="Microsoft.Extensions.DependencyInjection.IHttpClientBuilder"/> returned by <c>AddFoldersClient</c>.
/// </summary>
/// <remarks>
/// The token is sourced through a caller-supplied delegate so that no secret, token, or credential is ever
/// embedded in the sample. When the delegate returns a blank value, no <c>Authorization</c> header is added
/// (the unauthenticated run path is then exercised, which the server answers with a safe-denial envelope).
/// </remarks>
public sealed class BearerTokenHandler(Func<CancellationToken, ValueTask<string?>> tokenFactory) : DelegatingHandler
{
    private readonly Func<CancellationToken, ValueTask<string?>> _tokenFactory =
        tokenFactory ?? throw new ArgumentNullException(nameof(tokenFactory));

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? token = await _tokenFactory(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
