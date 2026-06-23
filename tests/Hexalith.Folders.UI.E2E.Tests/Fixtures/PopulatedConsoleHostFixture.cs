namespace Hexalith.Folders.UI.E2E.Tests.Fixtures;

using System.Diagnostics;
using System.Net.Http;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI;
using Hexalith.Folders.UI.Components;
using Hexalith.Folders.UI.Configuration;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Xunit;

/// <summary>
/// Story 8.4 — stands up the Folders operations console on Kestrel against a random localhost port like
/// <see cref="AspireConsoleHostFixture"/>, but registers a populated stub <see cref="IClient"/> so the
/// journey pages render their full read-only evidence surfaces (AD1). The axe / WCAG 2.2 AA scan's most
/// valuable additions over the bUnit sweep — real-browser color-contrast (1.4.3) and table / dense-identifier
/// semantics — require a fully-rendered populated DOM, which the dead-loopback hermetic host cannot produce.
/// </summary>
/// <remarks>
/// <para>
/// The single behavioural change from the hermetic host is the client registration:
/// <c>services.Replace(ServiceDescriptor.Scoped&lt;IClient&gt;(_ =&gt; stub))</c>. <c>IClient</c> is a typed
/// HttpClient registered <c>Transient</c> by <c>AddFoldersClient</c>; <see cref="ServiceCollectionDescriptorExtensions.Replace"/>
/// swaps that single descriptor (a <c>TryAdd</c> would lose to the real Transient and the SDK would 500), and
/// <c>Scoped</c> matches how Blazor Server resolves <c>[Inject] IClient</c> per-circuit (precedent:
/// the bUnit <c>DiagnosticTestContext</c>). The dead-loopback <c>Folders:Client:BaseAddress</c> is intentionally
/// not set — the stub replaces the SDK transport, so the <c>AddFoldersClient</c> base-address validator is never hit.
/// </para>
/// <para><c>EnvironmentName</c>, <c>ValidateScopes</c>, the random loopback port, and the readiness probe are kept identical.</para>
/// </remarks>
public abstract class PopulatedConsoleHostFixture : IAsyncLifetime
{
    private WebApplication? _app;
    private Uri? _baseAddress;

    /// <summary>The dataset density seeded into the stub <see cref="IClient"/> for this host.</summary>
    protected abstract ConsoleStubFixtures.Density Density { get; }

    /// <summary>The base address of the running console; valid only after <see cref="InitializeAsync"/>.</summary>
    public Uri BaseAddress => _baseAddress
        ?? throw new InvalidOperationException("PopulatedConsoleHostFixture has not completed InitializeAsync.");

    /// <inheritdoc />
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

        // AD1: replace the typed-HttpClient IClient with a populated synthetic stub so journey pages render
        // fully populated. Replace (not TryAdd) swaps the single descriptor; Scoped matches Blazor Server's
        // per-circuit [Inject] resolution. The stub is read-only configured returns — safe to share per scope.
        IClient stub = ConsoleStubFixtures.CreateClient(Density);
        builder.Services.Replace(ServiceDescriptor.Scoped<IClient>(_ => stub));

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

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync().ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
        }
    }
}
