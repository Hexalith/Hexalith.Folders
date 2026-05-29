namespace Hexalith.Folders.UI.E2E.Tests.Responsive;

using Hexalith.Folders.UI.E2E.Tests.Fixtures;
using Hexalith.Folders.UI.E2E.Tests.Routes;

using Microsoft.Playwright;

using Shouldly;

using Xunit;

/// <summary>
/// Story 6.11 / Task 3 / AC #8 — the (optional) non-brittle Playwright responsive-viewport smoke
/// (UX-DR31 / UX-DR28 / UX-DR29). It drives the four named long-path / dense-identifier surfaces —
/// <b>tables</b> (folders list, audit-trail, provider-support), <b>timelines</b> (operation-timeline,
/// incident-stream), <b>metadata trees</b> (folder-detail) and <b>trust summaries</b> (workspace) — across
/// desktop (1280), tablet (768) and the two mobile-fallback widths (~430 / ~360; ux-design-specification.md
/// L758-764) and asserts that at <b>every</b> width the page <b>root</b> still resolves, exactly one
/// <c>&lt;h1&gt;</c> renders, and the read-only boundary holds (zero mutation affordances) — so core lookup /
/// trust review is not broken by the responsive reflow.
/// <para>
/// This is <b>presence-only</b> by contract. The project forbids brittle pixel / overflow / CSS-class / text /
/// sleep assertions (project-context, AC #13), so the real-width <i>visual</i> assessment (does anything clip,
/// overlap, or lose function) stays documented manual evidence in
/// <c>docs/ux/ops-console-accessibility-and-no-mutation-verification.md</c>; this lane asserts only the stable
/// structural invariants. Against the backend-less hermetic host
/// (<see cref="AspireConsoleHostFixture"/>) the projection reads fail and the SDK-backed pages degrade to the
/// §3.8 read-model-unavailable state, so only the always-present page root + single heading + the
/// command-suppression DOM counts are asserted — the populated-surface test ids are intentionally not asserted
/// here (that would be flaky against a host with no projections), and the no-mutation / accessibility-structural
/// proofs over fully-populated surfaces are owned by the bUnit
/// <c>NoMutationConsoleSweepTests</c> / <c>AccessibilityContractSweepTests</c> sweeps.
/// </para>
/// <para>
/// The no-mutation DOM-count style mirrors <see cref="StateLabels.StateLabelGalleryE2ETests"/> (note the
/// hyphenated <c>fluent-dialog</c> custom-element selector in the real rendered DOM).
/// </para>
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class ResponsiveViewportSmokeTests : IClassFixture<AspireConsoleHostFixture>, IAsyncLifetime
{
    // Synthetic identifiers only (AC #12) — never real tenant / folder / workspace data.
    private const string FolderId = "folder-123";
    private const string WorkspaceId = "workspace-abc";

    private readonly PlaywrightFixture _playwright;
    private readonly AspireConsoleHostFixture _host;
    private IBrowserContext? _context;
    private IPage? _page;

    public ResponsiveViewportSmokeTests(PlaywrightFixture playwright, AspireConsoleHostFixture host)
    {
        _playwright = playwright;
        _host = host;
    }

    /// <summary>
    /// The route × viewport-width matrix. Each console surface that carries dense identifiers / long paths
    /// (tables, timelines, metadata trees, trust summaries — AC #8) is exercised at desktop, tablet and the
    /// two mobile-fallback widths.
    /// </summary>
    public static TheoryData<string, int, int> RouteViewportMatrix()
    {
        string[] routeKeys =
        [
            "folders",            // tables (folder discovery list)
            "folder-detail",      // metadata tree (MetadataOnlyFolderTree)
            "workspace",          // trust summary (WorkspaceTrustSummary + TrustMatrix)
            "audit-trail",        // table
            "operation-timeline", // timeline + table
            "provider-support",   // table / capability matrix
            "incident-stream",    // timeline + table (F-6 last-resort read path)
        ];

        (int Width, int Height)[] viewports =
        [
            (1280, 800),  // desktop
            (768, 1024),  // tablet (768-1023 band)
            (430, 932),   // mobile fallback (large)
            (360, 740),   // mobile fallback (small)
        ];

        TheoryData<string, int, int> data = [];
        foreach (string routeKey in routeKeys)
        {
            foreach ((int width, int height) in viewports)
            {
                data.Add(routeKey, width, height);
            }
        }

        return data;
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

    [Theory]
    [MemberData(nameof(RouteViewportMatrix))]
    public async Task ConsoleSurface_RemainsReadOnly_WithSingleHeading_AtViewportWidth(
        string routeKey,
        int width,
        int height)
    {
        IPage page = Page;

        // Set the viewport BEFORE navigating so the responsive layout reflows on first paint.
        // (Test-method awaits omit ConfigureAwait per xUnit1030 — only the IAsyncLifetime hooks use it.)
        await page.SetViewportSizeAsync(width, height);

        Uri target = new(_host.BaseAddress, ResolveRoute(routeKey));
        IResponse? response = await page.GotoAsync(target.ToString());

        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        // Presence-only structural invariants (UX-DR31): the page root resolves and exactly one <h1> renders
        // at every width — core lookup / trust review survives the tablet / mobile-fallback reflow.
        ILocator pageRoot = page.Locator($"[data-testid=\"{RootTestId(routeKey)}\"]");
        await pageRoot.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        (await page.Locator("h1").CountAsync())
            .ShouldBe(1, $"route '{routeKey}' should render exactly one <h1> at {width}x{height}");

        // The read-only boundary holds at every responsive width (UX-DR11 / UX-DR23 / F-2) — the
        // command-suppression DOM-count style of StateLabelGalleryE2ETests.
        (await page.Locator("form").CountAsync())
            .ShouldBe(0, $"route '{routeKey}' must expose no <form> at {width}x{height}");
        (await page.Locator("fluent-dialog").CountAsync())
            .ShouldBe(0, $"route '{routeKey}' must expose no fluent-dialog at {width}x{height}");
        (await page.Locator("[data-fc-command]").CountAsync())
            .ShouldBe(0, $"route '{routeKey}' must expose no [data-fc-command] at {width}x{height}");
        (await page.Locator("[data-fc-mutation]").CountAsync())
            .ShouldBe(0, $"route '{routeKey}' must expose no [data-fc-mutation] at {width}x{height}");
    }

    private static string ResolveRoute(string routeKey) => routeKey switch
    {
        "folders" => ConsoleRoutes.Folders,
        "folder-detail" => ConsoleRoutes.FolderDetail(FolderId),
        "workspace" => ConsoleRoutes.Workspace(FolderId, WorkspaceId),
        "audit-trail" => ConsoleRoutes.AuditTrail(FolderId),
        "operation-timeline" => ConsoleRoutes.OperationTimeline(FolderId),
        "provider-support" => ConsoleRoutes.ProviderSupport,
        "incident-stream" => ConsoleRoutes.IncidentStream(FolderId),
        _ => throw new ArgumentOutOfRangeException(nameof(routeKey), routeKey, "Unknown console route key."),
    };

    private static string RootTestId(string routeKey) => routeKey switch
    {
        "folders" => "console-page-folders-root",
        "folder-detail" => "console-page-folder-detail-root",
        "workspace" => "console-page-workspace-root",
        "audit-trail" => "console-page-audit-trail-root",
        "operation-timeline" => "console-page-operation-timeline-root",
        "provider-support" => "console-page-provider-support-root",
        "incident-stream" => "console-page-incident-stream-root",
        _ => throw new ArgumentOutOfRangeException(nameof(routeKey), routeKey, "Unknown console route key."),
    };

    private IPage Page => _page
        ?? throw new InvalidOperationException("The Playwright page has not completed InitializeAsync.");
}
