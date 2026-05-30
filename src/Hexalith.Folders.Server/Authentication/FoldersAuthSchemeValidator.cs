using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Server.Authentication;

/// <summary>
/// Refuses to start the Folders host outside non-production environments unless the
/// production JWT bearer scheme and OIDC pins are configured.
/// </summary>
public sealed class FoldersAuthSchemeValidator(
    IAuthenticationSchemeProvider schemeProvider,
    IHostEnvironment environment,
    IOptionsMonitor<FoldersOidcOptions> oidcOptions,
    IOptionsMonitor<JwtBearerOptions> jwtOptions) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Test"))
        {
            return;
        }

        IEnumerable<AuthenticationScheme> schemes = await schemeProvider.GetAllSchemesAsync().ConfigureAwait(false);
        AuthenticationScheme? bearer = schemes.SingleOrDefault(static scheme =>
            string.Equals(scheme.Name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal)
            && scheme.HandlerType == typeof(JwtBearerHandler));
        if (bearer is null)
        {
            throw new InvalidOperationException(
                "Hexalith.Folders.Server requires the JwtBearerDefaults.AuthenticationScheme JWT bearer scheme outside Development/Test. "
                + "Cookie, hermetic, or unrelated authentication schemes do not satisfy production readiness.");
        }

        FoldersOidcOptions oidc = oidcOptions.CurrentValue;
        JwtBearerOptions jwt = jwtOptions.Get(JwtBearerDefaults.AuthenticationScheme);
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(oidc.Audience) || string.IsNullOrWhiteSpace(jwt.TokenValidationParameters.ValidAudience))
        {
            failures.Add("audience pin");
        }

        if (string.IsNullOrWhiteSpace(oidc.ValidIssuer ?? oidc.Authority) || string.IsNullOrWhiteSpace(jwt.TokenValidationParameters.ValidIssuer))
        {
            failures.Add("issuer pin");
        }

        if (string.IsNullOrWhiteSpace(jwt.Authority) && string.IsNullOrWhiteSpace(jwt.MetadataAddress))
        {
            failures.Add("authority or metadata address");
        }

        if (!jwt.RequireHttpsMetadata)
        {
            failures.Add("HTTPS metadata requirement");
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "Hexalith.Folders.Server JWT bearer production readiness is incomplete: "
                + string.Join(", ", failures)
                + ".");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
