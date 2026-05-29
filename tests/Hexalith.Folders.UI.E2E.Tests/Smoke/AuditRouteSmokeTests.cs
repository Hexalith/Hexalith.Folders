namespace Hexalith.Folders.UI.E2E.Tests.Smoke;

using Hexalith.Folders.UI.E2E.Tests.Fixtures;
using Hexalith.Folders.UI.E2E.Tests.Routes;

using Microsoft.Playwright;

using Shouldly;

using Xunit;

/// <summary>
/// Story 6.8 / AC #1 / AC #2 / AC #12 — audit-route smoke tests following <see cref="ProviderRouteSmokeTests"/>.
/// Assert the folder-scoped audit-trail and operation-timeline routes load (200-399), expose their page
/// root, and render a single heading. Both pages read projections on load; against the backend-less
/// hermetic host the reads fail at the transport layer and the pages degrade to the §3.8
/// read-model-unavailable state — the page root and single heading still render (no crash), which is why
/// catching <c>HttpRequestException</c>/<c>TaskCanceledException</c> on the primary read is mandatory.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class AuditRouteSmokeTests : IClassFixture<AspireConsoleHostFixture>, IAsyncLifetime
{
    private readonly PlaywrightFixture _playwright;
    private readonly AspireConsoleHostFixture _host;
    private IBrowserContext? _context;
    private IPage? _page;

    public AuditRouteSmokeTests(PlaywrightFixture playwright, AspireConsoleHostFixture host)
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
    public async Task AuditTrailPageLoads_AndExposesConsolePageAuditTrailRoot()
    {
        _page.ShouldNotBeNull();

        Uri target = new(_host.BaseAddress, ConsoleRoutes.AuditTrail("smoke-folder"));
        IResponse? response = await _page.GotoAsync(target.ToString());

        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        ILocator pageRoot = _page.Locator("[data-testid=\"console-page-audit-trail-root\"]");
        await pageRoot.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        (await _page.Locator("h1").CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task OperationTimelinePageLoads_AndExposesConsolePageOperationTimelineRoot()
    {
        _page.ShouldNotBeNull();

        Uri target = new(_host.BaseAddress, ConsoleRoutes.OperationTimeline("smoke-folder"));
        IResponse? response = await _page.GotoAsync(target.ToString());

        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        ILocator pageRoot = _page.Locator("[data-testid=\"console-page-operation-timeline-root\"]");
        await pageRoot.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        (await _page.Locator("h1").CountAsync()).ShouldBe(1);
    }
}
