namespace Hexalith.Folders.UI.E2E.Tests.Smoke;

using Hexalith.Folders.UI.E2E.Tests.Fixtures;
using Hexalith.Folders.UI.E2E.Tests.Routes;

using Microsoft.Playwright;

using Shouldly;

using Xunit;

/// <summary>
/// Story 6.9 / F-6 / AC #15 — incident-route smoke test following <see cref="AuditRouteSmokeTests"/>. Asserts
/// the incident-stream route loads (200-399), exposes its page root, and renders a single heading. The page
/// reads the operation-timeline projection on load; against the backend-less hermetic host the read fails at
/// the transport layer and the page degrades to the §3.8 read-model-unavailable state — the page root, the
/// PERSISTENT degraded-mode banner, and the single heading still render (no crash), which is why catching
/// <c>HttpRequestException</c>/<c>TaskCanceledException</c> on the primary read is mandatory.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class IncidentRouteSmokeTests : IClassFixture<AspireConsoleHostFixture>, IAsyncLifetime
{
    private readonly PlaywrightFixture _playwright;
    private readonly AspireConsoleHostFixture _host;
    private IBrowserContext? _context;
    private IPage? _page;

    public IncidentRouteSmokeTests(PlaywrightFixture playwright, AspireConsoleHostFixture host)
    {
        _playwright = playwright;
        _host = host;
    }

    public async ValueTask InitializeAsync()
    {
        _context = await _playwright.Browser.NewContextAsync().ConfigureAwait(false);
        _page = await _context.NewPageAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_page is not null)
        {
            await _page.CloseAsync().ConfigureAwait(false);
        }

        if (_context is not null)
        {
            await _context.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task IncidentStreamPageLoads_AndExposesConsolePageIncidentStreamRoot()
    {
        _page.ShouldNotBeNull();

        Uri target = new(_host.BaseAddress, ConsoleRoutes.IncidentStream("smoke-folder"));
        IResponse? response = await _page.GotoAsync(target.ToString());

        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        ILocator pageRoot = _page.Locator("[data-testid=\"console-page-incident-stream-root\"]");
        await pageRoot.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        (await _page.Locator("h1").CountAsync()).ShouldBe(1);
    }
}
