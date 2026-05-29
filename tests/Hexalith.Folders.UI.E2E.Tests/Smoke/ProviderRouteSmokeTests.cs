namespace Hexalith.Folders.UI.E2E.Tests.Smoke;

using Hexalith.Folders.UI.E2E.Tests.Fixtures;
using Hexalith.Folders.UI.E2E.Tests.Routes;

using Microsoft.Playwright;

using Shouldly;

using Xunit;

/// <summary>
/// Story 6.7 / AC #1 / AC #12 — provider-route smoke tests following <see cref="FolderRouteSmokeTests"/>.
/// Assert the folder-scoped provider readiness route and the tenant-scoped provider support route load
/// (200-399), expose their page root, and render a single heading. Both pages read projections on load;
/// against the backend-less hermetic host the reads fail at the transport layer and the pages degrade to
/// the §3.8 read-model-unavailable state — the page root and single heading still render (no crash).
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class ProviderRouteSmokeTests : IClassFixture<AspireConsoleHostFixture>, IAsyncLifetime
{
    private readonly PlaywrightFixture _playwright;
    private readonly AspireConsoleHostFixture _host;
    private IBrowserContext? _context;
    private IPage? _page;

    public ProviderRouteSmokeTests(PlaywrightFixture playwright, AspireConsoleHostFixture host)
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
    public async Task ProviderPageLoads_AndExposesConsolePageProviderRoot()
    {
        _page.ShouldNotBeNull();

        Uri target = new(_host.BaseAddress, ConsoleRoutes.Provider("smoke-folder"));
        IResponse? response = await _page.GotoAsync(target.ToString());

        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        ILocator pageRoot = _page.Locator("[data-testid=\"console-page-provider-root\"]");
        await pageRoot.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        (await _page.Locator("h1").CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task ProviderPageWithOperationContext_Loads_AndExposesConsolePageProviderRoot()
    {
        _page.ShouldNotBeNull();

        // The optional workspace/operation context arrives via the query string ([SupplyParameterFromQuery]);
        // supplying it triggers the additional supplementary reads on load. Against the backend-less hermetic
        // host those reads fail at transport and the page degrades to the §3.8 read-model-unavailable state —
        // but it must not crash composing/parsing the query parameters: the page root and single heading
        // still render (AC #5 honest-Unknown path is reachable, AC #12 no-crash on load).
        Uri target = new(_host.BaseAddress, ConsoleRoutes.Provider("smoke-folder") + "?WorkspaceId=smoke-ws&OperationId=smoke-op");
        IResponse? response = await _page.GotoAsync(target.ToString());

        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        ILocator pageRoot = _page.Locator("[data-testid=\"console-page-provider-root\"]");
        await pageRoot.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        (await _page.Locator("h1").CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task ProviderSupportPageLoads_AndExposesConsolePageProviderSupportRoot()
    {
        _page.ShouldNotBeNull();

        Uri target = new(_host.BaseAddress, ConsoleRoutes.ProviderSupport);
        IResponse? response = await _page.GotoAsync(target.ToString());

        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        ILocator pageRoot = _page.Locator("[data-testid=\"console-page-provider-support-root\"]");
        await pageRoot.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        (await _page.Locator("h1").CountAsync()).ShouldBe(1);
    }
}
