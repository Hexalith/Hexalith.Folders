namespace Hexalith.Folders.UI.E2E.Tests.Accessibility;

using System.Globalization;

using Hexalith.Folders.UI.E2E.Tests.Fixtures;
using Hexalith.Folders.UI.E2E.Tests.Routes;

using Microsoft.Playwright;

using Shouldly;

using Xunit;

/// <summary>
/// Story 8.4 / AC #3 — the UX-DR31 zoom (125 % / 150 % / 200 %) + dense-identifier no-horizontal-clipping
/// invariant the presence-only <c>ResponsiveViewportSmokeTests</c> omits (it sets viewports before navigate and
/// never zooms, never asserts no-clipping). Run against the dense-identifier populated host
/// (<see cref="DenseIdentifierConsoleHostFixture"/>) over each journey terminal surface — tables, timelines,
/// metadata trees, and trust summaries.
/// <para>
/// Browser zoom is emulated faithfully by reducing the layout viewport to the zoomed CSS width
/// (<c>effective width = base / zoom</c>), applied <b>after</b> render (unlike the responsive smoke). The
/// assertion is a semantic invariant, never pixel geometry (project AC #13 / AD6):
/// <list type="number">
///   <item>at every zoom level the page-root and the key surface stay <b>visible and un-clipped</b> — their left
///   edge remains within the viewport, so content is never cut off / hidden (horizontal scroll of a wide
///   multi-column data table is reachable content, not clipping — WCAG 1.4.10's two-dimensional-data exemption);</item>
///   <item>at the 200 % reflow target the responsive layout has fully reflowed so there is <b>zero</b>
///   document-level horizontal overflow (the WCAG 1.4.10 reflow proof).</item>
/// </list>
/// </para>
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class ConsoleZoomReflowGateTests : IClassFixture<DenseIdentifierConsoleHostFixture>, IAsyncLifetime
{
    private const int BaseWidth = 1280;
    private const int BaseHeight = 800;

    // Browser-zoom levels (UX-DR31). The effective CSS viewport width is the base width divided by the zoom
    // factor — exactly what a real browser zoom does to the layout viewport.
    private static readonly double[] ZoomLevels = [1.25, 1.50, 2.00];

    // The 200 % reflow target: the layout has stacked every surface, so zero document-level horizontal overflow
    // is required. A couple of px of rounding tolerance only.
    private const double ReflowTarget = 2.00;
    private const int OverflowTolerance = 2;

    // Reads document overflow + the key surface's left edge / visibility under the current (zoomed) viewport.
    // Delimited (not JSON) to avoid serializer property-name coupling. EvaluateAsync forces a layout flush, so
    // the values reflect the resized viewport without an arbitrary wait.
    private const string ReflowProbe = @"(sel) => {
  const de = document.documentElement;
  const el = document.querySelector('[data-testid=""' + sel + '""]');
  const rects = el ? el.getClientRects() : [];
  const left = rects.length ? Math.round(rects[0].left) : 999999;
  const visible = rects.length > 0;
  return de.scrollWidth + '|' + de.clientWidth + '|' + left + '|' + visible;
}";

    private readonly PlaywrightFixture _playwright;
    private readonly DenseIdentifierConsoleHostFixture _host;
    private IBrowserContext? _context;
    private IPage? _page;

    public ConsoleZoomReflowGateTests(PlaywrightFixture playwright, DenseIdentifierConsoleHostFixture host)
    {
        _playwright = playwright;
        _host = host;
    }

    /// <summary>The UX-DR31 terminal surfaces (route key, page-content root, key surface) under dense-identifier stress.</summary>
    public static TheoryData<string, string, string> TerminalSurfaces() => new()
    {
        { "folder-detail", "console-page-folder-detail-root", "console-page-folder-detail-identity" }, // metadata tree / identity
        { "workspace", "console-page-workspace-root", "workspace-trust-summary" },                     // trust summary
        { "audit-trail", "console-page-audit-trail-root", "console-page-audit-trail-table" },          // table
        { "operation-timeline", "console-page-operation-timeline-root", "console-page-operation-timeline-table" }, // timeline
        { "incident-stream", "console-page-incident-stream-root", "console-page-incident-stream-table" }, // F-6 timeline
        { "provider-support", "console-page-provider-support-root", "console-page-provider-support-matrix" }, // capability matrix
        { "provider", "console-page-provider-root", "console-page-provider-section-identity" },          // folder-scoped provider readiness (dense binding refs)
    };

    public async ValueTask InitializeAsync()
    {
        _context = await _playwright.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = BaseWidth, Height = BaseHeight },
        }).ConfigureAwait(false);
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

    [Theory]
    [MemberData(nameof(TerminalSurfaces))]
    public async Task ConsoleTerminalSurface_DoesNotClip_AtZoom_AndReflowsAt200(
        string routeKey,
        string rootTestId,
        string keyTestId)
    {
        IPage page = Page;

        await page.SetViewportSizeAsync(BaseWidth, BaseHeight);
        Uri target = new(_host.BaseAddress, ResolveRoute(routeKey));
        IResponse? response = await page.GotoAsync(target.ToString());
        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        ILocator root = page.Locator($"[data-testid=\"{rootTestId}\"]");
        ILocator key = page.Locator($"[data-testid=\"{keyTestId}\"]");
        await key.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        foreach (double zoom in ZoomLevels)
        {
            // Emulate browser zoom AFTER render by reducing the layout viewport to the zoomed CSS width.
            int effectiveWidth = (int)Math.Round(BaseWidth / zoom, MidpointRounding.AwayFromZero);
            await page.SetViewportSizeAsync(effectiveWidth, BaseHeight);

            (await root.IsVisibleAsync()).ShouldBeTrue(Because(routeKey, zoom, "page-root must stay visible"));
            (await key.IsVisibleAsync()).ShouldBeTrue(Because(routeKey, zoom, $"key surface '{keyTestId}' must stay visible"));

            string probe = await page.EvaluateAsync<string>(ReflowProbe, keyTestId);
            string[] parts = probe.Split('|', 4);
            int scrollWidth = int.Parse(parts[0], CultureInfo.InvariantCulture);
            int clientWidth = int.Parse(parts[1], CultureInfo.InvariantCulture);
            int keyLeft = int.Parse(parts[2], CultureInfo.InvariantCulture);
            bool keyHasBox = bool.Parse(parts[3]);

            // Not clipped: the key surface keeps a layout box and its left edge stays within the viewport
            // (content reachable, never cut off the left — wide-table horizontal scroll is reachable content).
            keyHasBox.ShouldBeTrue(Because(routeKey, zoom, $"key surface '{keyTestId}' must keep a layout box"));
            keyLeft.ShouldBeGreaterThanOrEqualTo(-OverflowTolerance, Because(routeKey, zoom, "key surface must not be clipped off the left edge"));
            keyLeft.ShouldBeLessThan(clientWidth, Because(routeKey, zoom, "key surface must start within the layout viewport"));

            if (Math.Abs(zoom - ReflowTarget) < 0.001)
            {
                // WCAG 1.4.10 reflow proof: at the 200 % target every surface has reflowed — no horizontal scroll.
                scrollWidth.ShouldBeLessThanOrEqualTo(clientWidth + OverflowTolerance,
                    Because(routeKey, zoom, $"document must fully reflow with no horizontal overflow (scrollWidth={scrollWidth}, clientWidth={clientWidth})"));
            }
        }
    }

    private static string Because(string routeKey, double zoom, string detail)
        => string.Create(CultureInfo.InvariantCulture, $"route '{routeKey}' at {zoom * 100:0}% zoom: {detail}.");

    private static string ResolveRoute(string routeKey) => routeKey switch
    {
        "folder-detail" => ConsoleRoutes.FolderDetail(ConsoleStubFixtures.FolderId),
        "workspace" => ConsoleRoutes.Workspace(ConsoleStubFixtures.FolderId, ConsoleStubFixtures.WorkspaceId),
        "audit-trail" => ConsoleRoutes.AuditTrail(ConsoleStubFixtures.FolderId),
        "operation-timeline" => ConsoleRoutes.OperationTimeline(ConsoleStubFixtures.FolderId),
        "incident-stream" => ConsoleRoutes.IncidentStream(ConsoleStubFixtures.FolderId),
        "provider-support" => ConsoleRoutes.ProviderSupport,
        "provider" => ConsoleRoutes.Provider(ConsoleStubFixtures.FolderId),
        _ => throw new ArgumentOutOfRangeException(nameof(routeKey), routeKey, "Unknown console route key."),
    };

    private IPage Page => _page
        ?? throw new InvalidOperationException("The Playwright page has not completed InitializeAsync.");
}
