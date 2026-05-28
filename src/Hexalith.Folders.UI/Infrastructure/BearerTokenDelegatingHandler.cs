using System.Net.Http.Headers;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.UI.Infrastructure;

/// <summary>
/// Forwards the OIDC access token captured on the inbound circuit's <see cref="HttpContext"/>
/// onto outbound SDK requests as an <c>Authorization: Bearer &lt;token&gt;</c> header. When no
/// token is available the request is sent without an <c>Authorization</c> header — the Folders
/// server then fails-closed with a canonical 401 / safe-denial envelope, which is the desired
/// behaviour for unauthenticated calls.
/// </summary>
internal sealed class BearerTokenDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BearerTokenDelegatingHandler(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            string? token = await httpContext
                .GetTokenAsync("access_token")
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }
}
