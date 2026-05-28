namespace Hexalith.Folders.UI.E2E.Tests.Smoke;

using Hexalith.Folders.UI.E2E.Tests.Fixtures;
using Hexalith.Folders.UI.E2E.Tests.Routes;

using Microsoft.Playwright;

using Shouldly;

using Xunit;

[Collection(PlaywrightCollection.Name)]
public sealed class ConsoleSmokeTests : IClassFixture<AspireConsoleHostFixture>, IAsyncLifetime
{
    private readonly PlaywrightFixture _playwright;
    private readonly AspireConsoleHostFixture _host;
    private IBrowserContext? _context;
    private IPage? _page;

    public ConsoleSmokeTests(PlaywrightFixture playwright, AspireConsoleHostFixture host)
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
    public async Task HomePageLoads_AndExposesConsolePageHomeRoot()
    {
        _page.ShouldNotBeNull();

        Uri target = new(_host.BaseAddress, ConsoleRoutes.Home);
        IResponse? response = await _page.GotoAsync(target.ToString());

        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        ILocator pageRoot = _page.Locator("[data-testid=\"console-page-home-root\"]");
        await pageRoot.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        string headingText = await _page.Locator("h1").InnerTextAsync();
        headingText.ShouldContain("Operations Console");
    }
}
