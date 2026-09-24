using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders.IntegrationTests.Routing;

/// <summary>
/// Test authentication scheme that turns an <c>Authorization: RoutingTest</c> request into the claims the real
/// claims-based tenant and EventStore evidence accessors read. The principal exists only if
/// <c>UseAuthentication</c> ran before the component that reads it.
/// </summary>
/// <param name="options">The scheme options monitor.</param>
/// <param name="logger">The logger factory.</param>
/// <param name="encoder">The URL encoder.</param>
internal sealed class RoutingModeAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>The scheme name.</summary>
    public const string SchemeName = "RoutingTest";

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!string.Equals(Request.Headers.Authorization.ToString(), SchemeName, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        ClaimsIdentity identity = new(
            [
                new Claim("eventstore:tenant", RoutingModeTestHost.TenantId),
                new Claim("sub", RoutingModeTestHost.PrincipalId),
                new Claim("eventstore:permission", "read_metadata"),
            ],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
