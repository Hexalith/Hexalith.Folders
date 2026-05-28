namespace Hexalith.Folders.UI.E2E.Tests.Smoke;

using Hexalith.Folders.UI.E2E.Tests.Fixtures;
using Hexalith.Folders.UI.E2E.Tests.Routes;

using Microsoft.Playwright;

using Shouldly;

using Xunit;

/// <summary>
/// Story 6.6 / AC #1 / AC #12 — folder-route smoke test following <see cref="ConsoleSmokeTests"/>.
/// Asserts the discovery entry route loads (200-399), exposes its page root, and renders a single
/// heading. The folder list page is self-contained (no backend read on load).
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class FolderRouteSmokeTests : IClassFixture<AspireConsoleHostFixture>, IAsyncLifetime
{
    private readonly PlaywrightFixture _playwright;
    private readonly AspireConsoleHostFixture _host;
    private IBrowserContext? _context;
    private IPage? _page;

    public FolderRouteSmokeTests(PlaywrightFixture playwright, AspireConsoleHostFixture host)
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
    public async Task FoldersPageLoads_AndExposesConsolePageFoldersRoot()
    {
        _page.ShouldNotBeNull();

        Uri target = new(_host.BaseAddress, ConsoleRoutes.Folders);
        IResponse? response = await _page.GotoAsync(target.ToString());

        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        ILocator pageRoot = _page.Locator("[data-testid=\"console-page-folders-root\"]");
        await pageRoot.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        (await _page.Locator("h1").CountAsync()).ShouldBe(1);
    }
}
