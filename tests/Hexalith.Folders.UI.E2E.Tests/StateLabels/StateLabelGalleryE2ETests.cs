namespace Hexalith.Folders.UI.E2E.Tests.StateLabels;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.E2E.Tests.Fixtures;
using Hexalith.Folders.UI.E2E.Tests.Routes;

using Microsoft.Playwright;

using Shouldly;

using Xunit;

[Collection(PlaywrightCollection.Name)]
public sealed class StateLabelGalleryE2ETests : IClassFixture<AspireConsoleHostFixture>, IAsyncLifetime
{
    private readonly PlaywrightFixture _playwright;
    private readonly AspireConsoleHostFixture _host;
    private IBrowserContext? _context;
    private IPage? _page;

    public StateLabelGalleryE2ETests(PlaywrightFixture playwright, AspireConsoleHostFixture host)
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
    public async Task StateLabelGallery_Loads_AndExposesReusableStatusSelectors()
    {
        IPage page = Page;

        Uri target = new(_host.BaseAddress, ConsoleRoutes.StateLabelGallery);
        IResponse? response = await page.GotoAsync(target.ToString());

        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        ILocator pageRoot = page.Locator("[data-testid=\"console-page-state-label-gallery-root\"]");
        await pageRoot.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        int expectedRows = Enum.GetValues<LifecycleState>().Length + 1;
        int technicalStateCount = await page.Locator("[data-testid=\"technical-state-metadata\"]").CountAsync();
        int dispositionBadgeCount = await page.Locator("[data-testid=\"operator-disposition-badge\"]").CountAsync();
        int slotCount = await page.Locator("[data-fc-badge-slot]").CountAsync();

        technicalStateCount.ShouldBe(expectedRows);
        dispositionBadgeCount.ShouldBe(expectedRows);
        slotCount.ShouldBe(expectedRows);
    }

    [Fact]
    public async Task StateLabelGallery_RendersReadyProjectionLagBranch_AsDistinctOperatorDisposition()
    {
        IPage page = Page;

        Uri target = new(_host.BaseAddress, ConsoleRoutes.StateLabelGallery);
        IResponse? response = await page.GotoAsync(target.ToString());

        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        ILocator readyStates = page.Locator("[data-testid=\"technical-state-metadata\"][data-fc-technical-state=\"ready\"]");
        await readyStates.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        int readyStateCount = await readyStates.CountAsync();
        readyStateCount.ShouldBe(2);

        IReadOnlyList<string> dispositionTexts = await page
            .Locator("[data-testid=\"operator-disposition-badge\"]")
            .AllInnerTextsAsync();

        dispositionTexts.Any(text => text.Contains("Available", StringComparison.Ordinal)).ShouldBeTrue();
        dispositionTexts.Any(text => text.Contains("Degraded but serving", StringComparison.Ordinal)).ShouldBeTrue();

        int successSlotCount = await page.Locator("[data-fc-badge-slot=\"Success\"]").CountAsync();
        int warningSlotCount = await page.Locator("[data-fc-badge-slot=\"Warning\"]").CountAsync();

        successSlotCount.ShouldBeGreaterThanOrEqualTo(1);
        warningSlotCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task StateLabelGallery_RendersWithoutMutationAffordances()
    {
        IPage page = Page;

        Uri target = new(_host.BaseAddress, ConsoleRoutes.StateLabelGallery);
        IResponse? response = await page.GotoAsync(target.ToString());

        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        await page.Locator("[data-testid=\"console-page-state-label-gallery-root\"]")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        int formCount = await page.Locator("form").CountAsync();
        int dialogCount = await page.Locator("fluent-dialog").CountAsync();
        int commandCount = await page.Locator("[data-fc-command]").CountAsync();
        int mutationCount = await page.Locator("[data-fc-mutation]").CountAsync();

        formCount.ShouldBe(0);
        dialogCount.ShouldBe(0);
        commandCount.ShouldBe(0);
        mutationCount.ShouldBe(0);
    }

    [Fact]
    public async Task HomePage_DoesNotAdvertiseStateLabelGallery_WhenHostIsNotDevelopment()
    {
        IPage page = Page;

        Uri target = new(_host.BaseAddress, ConsoleRoutes.Home);
        IResponse? response = await page.GotoAsync(target.ToString());

        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        await page.Locator("[data-testid=\"console-page-home-root\"]")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        int galleryLinkCount = await page.Locator($"a[href=\"{ConsoleRoutes.StateLabelGallery}\"]").CountAsync();
        galleryLinkCount.ShouldBe(0);
    }

    private IPage Page => _page
        ?? throw new InvalidOperationException("The Playwright page has not completed InitializeAsync.");
}
