using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Testing;

/// <summary>Supplies an explicit human transport principal to isolated handler fixtures with separately injected domain authority.</summary>
/// <param name="options">The fixture scheme options.</param>
/// <param name="logger">The fixture logger factory.</param>
/// <param name="encoder">The URL encoder.</param>
internal sealed class FoldersIsolatedHumanAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        ClaimsPrincipal principal = Context.User.Identity?.IsAuthenticated == true
            ? Context.User
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "isolated-handler-fixture")], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
