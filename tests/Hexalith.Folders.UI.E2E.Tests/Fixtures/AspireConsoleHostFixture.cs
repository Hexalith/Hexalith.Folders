namespace Hexalith.Folders.UI.E2E.Tests.Fixtures;

using System.Diagnostics;
using System.Net.Http;

using Hexalith.Folders.UI;
using Hexalith.Folders.UI.Components;
using Hexalith.Folders.UI.Configuration;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

/// <summary>
/// Stands up the Folders operations console deterministically for the UI E2E lane. The fixture
/// boots <see cref="Hexalith.Folders.UI"/> directly on Kestrel against a random localhost port
/// with <c>Folders:Authentication:Mode=hermetic-test</c> and the <c>Test</c> environment. Aspire
/// orchestration (Keycloak, sidecars, EventStore) is intentionally bypassed — the Story 6.2 smoke
/// only renders the home page and does not exercise projection calls, so a full distributed-host
/// boot would add flakiness without verifying anything new.
/// </summary>
/// <remarks>
/// Honors AC #6's "hermetic-test branch is rejected at boot unless ASPNETCORE_ENVIRONMENT in
/// {Development, Test}" gate by setting the environment to <c>Test</c> at boot.
/// </remarks>
public sealed class AspireConsoleHostFixture : IAsyncLifetime
{
    private WebApplication? _app;
    private Uri? _baseAddress;

    public Uri BaseAddress => _baseAddress
        ?? throw new InvalidOperationException("AspireConsoleHostFixture has not completed InitializeAsync.");

    public async ValueTask InitializeAsync()
    {
        WebApplicationOptions options = new()
        {
            EnvironmentName = CompositionRoot.TestEnvironmentName,
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
        };
        WebApplicationBuilder builder = WebApplication.CreateBuilder(options);

        builder.Host.UseDefaultServiceProvider(o => o.ValidateScopes = true);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{FoldersAuthenticationOptions.SectionName}:Mode"] = FoldersAuthenticationOptions.HermeticTestMode,
        });

        // Bind Kestrel to a random localhost port so Playwright can hit the running host.
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        CompositionRoot.ConfigureServices(builder.Services, builder.Configuration, builder.Environment);

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapStaticAssets();
        app.UseStaticFiles();
        app.UseRequestLocalization();
        app.UseAntiforgery();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        await app.StartAsync().ConfigureAwait(false);
        _app = app;

        Microsoft.AspNetCore.Hosting.Server.IServer server = app.Services
            .GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
        Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature? addresses = server.Features
            .Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
        string? address = addresses?.Addresses.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException("Kestrel did not surface a server address.");
        }

        _baseAddress = new Uri(address, UriKind.Absolute);

        // Lightweight readiness probe — Kestrel reports ready before /api/_blazor is reachable.
        using HttpClient probe = new() { Timeout = TimeSpan.FromSeconds(2) };
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            try
            {
                using HttpResponseMessage response = await probe
                    .GetAsync(new Uri(_baseAddress, "/"))
                    .ConfigureAwait(false);
                if ((int)response.StatusCode < 500)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Retry briefly while Kestrel finalizes startup.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Operations console did not reach a ready state within 30 seconds.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync().ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
        }
    }
}
