namespace Hexalith.Folders.UI.E2E.Tests.Accessibility;

using System.Text;

using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;

using Hexalith.Folders.UI.E2E.Tests.Fixtures;
using Hexalith.Folders.UI.E2E.Tests.Routes;

using Microsoft.Playwright;

using Shouldly;

using Xunit;

/// <summary>
/// Story 8.4 / AC #1 / AC #2 — the axe-core WCAG 2.2 AA scan across the three critical operations-console
/// journeys, run against the fully-populated host (<see cref="AccessibilityConsoleHostFixture"/>, AD1):
/// <list type="bullet">
///   <item><b>J1 find-and-inspect-trust-state:</b> <c>/folders</c> → folder detail → workspace (trust summary + matrix).</item>
///   <item><b>J2 prove-tenant-isolation:</b> folder detail (tenant banner + metadata-only tree) → provider support / provider.</item>
///   <item><b>J3 diagnose-failure-from-evidence:</b> audit trail + operation timeline + incident stream (F-6).</item>
/// </list>
/// axe is filtered to the cumulative WCAG AA tag set (<c>wcag2a, wcag2aa, wcag21a, wcag21aa, wcag22aa</c>) and the
/// test fails on <b>any</b> AA-tagged violation (AD3 — stricter than the README's serious/critical wording). This
/// covers axe's auto-detectable AA subset (contrast 1.4.3, name/role/value, semantic headings/landmarks, table
/// structure, link/control names); keyboard operability and visible focus are owned by
/// <see cref="ConsoleKeyboardFocusGateTests"/>, zoom / no-clipping by <see cref="ConsoleZoomReflowGateTests"/>, and
/// the not-color-alone sweeps by the bUnit <c>AccessibilityContractSweepTests</c> (the gate is a UNION, AD2).
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class ConsoleAxeWcagGateTests : IClassFixture<AccessibilityConsoleHostFixture>, IAsyncLifetime
{
    /// <summary>The cumulative WCAG 2.0/2.1/2.2 level-A + AA tag set axe is filtered to (AC #1).</summary>
    private static readonly AxeRunOptions WcagAaOptions = new()
    {
        RunOnly = new RunOnlyOptions
        {
            Type = "tag",
            Values = new List<string> { "wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa" },
        },
    };

    private readonly PlaywrightFixture _playwright;
    private readonly AccessibilityConsoleHostFixture _host;
    private IBrowserContext? _context;
    private IPage? _page;

    public ConsoleAxeWcagGateTests(PlaywrightFixture playwright, AccessibilityConsoleHostFixture host)
    {
        _playwright = playwright;
        _host = host;
    }

    /// <summary>
    /// Each distinct read-only route on the three critical journeys, paired with the populated <c>data-testid</c>
    /// to wait for before scanning. Routes come from <see cref="ConsoleRoutes"/> only (the lane's route contract).
    /// </summary>
    public static TheoryData<string> JourneyRoutes() =>
    [
        "folders",            // J1 entry — discovery
        "folder-detail",      // J1 + J2 — metadata-only folder tree / tenant banner
        "workspace",          // J1 terminal — trust summary + trust matrix
        "provider-support",   // J2 — tenant-scoped capability matrix
        "provider",           // J2 — folder-scoped provider readiness
        "audit-trail",        // J3 — audit table
        "operation-timeline", // J3 — diagnostic timeline table
        "incident-stream",    // J3 — F-6 last-resort read
    ];

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
    [MemberData(nameof(JourneyRoutes))]
    public async Task ConsoleJourneyRoute_HasNoWcag22AaViolations(string routeKey)
    {
        IPage page = Page;

        Uri target = new(_host.BaseAddress, ResolveRoute(routeKey));
        IResponse? response = await page.GotoAsync(target.ToString());

        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        // Wait for the populated surface so axe scans the rendered evidence DOM, not a pre-data skeleton.
        ILocator populated = page.Locator($"[data-testid=\"{PopulatedTestId(routeKey)}\"]");
        await populated.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Scope the scan to the console page-content root — the surface Story 8.4 owns and the bUnit
        // AccessibilityContractSweepTests render in isolation. The shared FrontComposer shell chrome
        // (nav toggle, command palette, settings) renders Fluent UI 5.0-RC <fluent-button> web components
        // whose light-DOM host carries no role attribute, which axe-core flags as aria-prohibited-attr — a
        // known axe-vs-web-component false positive (the shadow-DOM button has role=button and the aria-label
        // is announced). That chrome is FrontComposer infra, out of this story's scope (AC5); scoping here
        // keeps the FULL WCAG AA ruleset over the console content with no rule masking (AD3).
        ILocator pageRoot = page.Locator($"[data-testid=\"{RootTestId(routeKey)}\"]");
        AxeResult result = await pageRoot.RunAxe(WcagAaOptions);

        result.Violations.ShouldBeEmpty(
            $"route '{routeKey}' must have no WCAG 2.2 AA axe violations.{FormatViolations(result)}");
    }

    /// <summary>
    /// Metadata-only violation summary: rule id + impact + helpUrl + target selector only — never
    /// <c>node.Html</c> (the synthetic data is safe today; the discipline holds regardless, per the
    /// metadata-only invariant).
    /// </summary>
    private static string FormatViolations(AxeResult result)
    {
        if (result.Violations.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        builder.Append('\n');
        foreach (AxeResultItem violation in result.Violations)
        {
            string targets = string.Join("; ", violation.Nodes.Select(static node => node.Target?.ToString()));
            builder.Append("- ").Append(violation.Id)
                .Append(" [").Append(violation.Impact).Append("] ")
                .Append(violation.HelpUrl)
                .Append(" -> ").Append(targets)
                .Append('\n');
        }

        return builder.ToString();
    }

    private static string ResolveRoute(string routeKey) => routeKey switch
    {
        "folders" => ConsoleRoutes.Folders,
        "folder-detail" => ConsoleRoutes.FolderDetail(ConsoleStubFixtures.FolderId),
        "workspace" => ConsoleRoutes.Workspace(ConsoleStubFixtures.FolderId, ConsoleStubFixtures.WorkspaceId),
        "provider-support" => ConsoleRoutes.ProviderSupport,
        "provider" => ConsoleRoutes.Provider(ConsoleStubFixtures.FolderId),
        "audit-trail" => ConsoleRoutes.AuditTrail(ConsoleStubFixtures.FolderId),
        "operation-timeline" => ConsoleRoutes.OperationTimeline(ConsoleStubFixtures.FolderId),
        "incident-stream" => ConsoleRoutes.IncidentStream(ConsoleStubFixtures.FolderId),
        _ => throw new ArgumentOutOfRangeException(nameof(routeKey), routeKey, "Unknown console route key."),
    };

    /// <summary>The console page-content root <c>data-testid</c> the axe scan is scoped to.</summary>
    private static string RootTestId(string routeKey) => routeKey switch
    {
        "folders" => "console-page-folders-root",
        "folder-detail" => "console-page-folder-detail-root",
        "workspace" => "console-page-workspace-root",
        "provider-support" => "console-page-provider-support-root",
        "provider" => "console-page-provider-root",
        "audit-trail" => "console-page-audit-trail-root",
        "operation-timeline" => "console-page-operation-timeline-root",
        "incident-stream" => "console-page-incident-stream-root",
        _ => throw new ArgumentOutOfRangeException(nameof(routeKey), routeKey, "Unknown console route key."),
    };

    /// <summary>The populated-surface <c>data-testid</c> that appears only once the page has rendered evidence.</summary>
    private static string PopulatedTestId(string routeKey) => routeKey switch
    {
        "folders" => "console-page-folders-root",
        "folder-detail" => "console-page-folder-detail-identity",
        "workspace" => "workspace-trust-summary",
        "provider-support" => "console-page-provider-support-matrix",
        "provider" => "console-page-provider-section-identity",
        "audit-trail" => "console-page-audit-trail-table",
        "operation-timeline" => "console-page-operation-timeline-table",
        "incident-stream" => "console-page-incident-stream-table",
        _ => throw new ArgumentOutOfRangeException(nameof(routeKey), routeKey, "Unknown console route key."),
    };

    private IPage Page => _page
        ?? throw new InvalidOperationException("The Playwright page has not completed InitializeAsync.");
}
