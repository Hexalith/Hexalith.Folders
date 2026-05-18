using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.Server.Authentication;

/// <summary>
/// Refuses to start the Folders host when no authentication scheme is registered outside
/// the Development environment. JWT/OIDC wiring lives in a later story (production OIDC and
/// secret-store integration), but defense in depth requires that we never deploy with the
/// projection-store check as the sole gate against forged tenant ids.
/// </summary>
public sealed class FoldersAuthSchemeValidator(
    IAuthenticationSchemeProvider schemeProvider,
    IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        IEnumerable<AuthenticationScheme> schemes = await schemeProvider.GetAllSchemesAsync().ConfigureAwait(false);
        if (!schemes.Any())
        {
            throw new InvalidOperationException(
                "Hexalith.Folders.Server requires at least one authentication scheme registered outside the Development environment. "
                + "Register a JWT bearer (or equivalent) scheme that emits the configured tenant claim before starting the host.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
