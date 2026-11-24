namespace Hexalith.Folders.UI.E2E.Tests.Smoke;

using Hexalith.Folders.UI.E2E.Tests.Fixtures;
using Hexalith.Folders.UI.E2E.Tests.Routes;

using Microsoft.Playwright;

using Shouldly;

using Xunit;

/// <summary>
/// Drives a server-handled interaction on the folder discovery page after the Blazor Server circuit has connected.
/// The other smoke tests check prerendered markup; this one proves the circuit receives the input and the click and
/// performs the server-side navigation to the folder detail route.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class FolderNavigationInteractionTests : IClassFixture<InteractiveConsoleHostFixture>, IAsyncLifetime
{
    private const string FolderId = "folder-e2e-1";

    private readonly PlaywrightFixture _playwright;
    private readonly InteractiveConsoleHostFixture _host;
    private IBrowserContext? _context;
    private IPage? _page;

    public FolderNavigationInteractionTests(PlaywrightFixture playwright, InteractiveConsoleHostFixture host)
    {
        _playwright = playwright;
        _host = host;
    }

    public async ValueTask InitializeAsync()
    {
        _context = await _playwright.CreateAuthenticatedContextAsync(_host.BaseAddress).ConfigureAwait(false);
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
    public async Task OpenFolder_AfterTheCircuitConnects_NavigatesToTheFolderDetail()
    {
        _page.ShouldNotBeNull();

        IResponse? response = await BlazorCircuit.GotoAndWaitForFirstRenderAsync(
            _page,
            new Uri(_host.BaseAddress, ConsoleRoutes.Folders).ToString(),
            TestContext.Current.CancellationToken);
        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        await _page.Locator("[data-testid=\"console-page-folders-id-input\"]").FillAsync(FolderId);
        await _page.Locator("[data-testid=\"console-page-folders-open\"]").ClickAsync();

        await _page.WaitForURLAsync(new Uri(_host.BaseAddress, ConsoleRoutes.FolderDetail(FolderId)).ToString());
        await _page.Locator("[data-testid=\"console-page-folder-detail-root\"]")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        (await _page.Locator("h1").CountAsync()).ShouldBe(1);
    }
}
