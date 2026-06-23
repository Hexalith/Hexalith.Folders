namespace Hexalith.Folders.UI.E2E.Tests.Accessibility;

using System.Globalization;

using Hexalith.Folders.UI.E2E.Tests.Fixtures;
using Hexalith.Folders.UI.E2E.Tests.Routes;

using Microsoft.Playwright;

using Shouldly;

using Xunit;

/// <summary>
/// Story 8.4 / AC #2 — keyboard-operability + visible-focus assertions over <b>every route on the three critical
/// journeys</b> (UX-DR30: search, filters, result selection, tabs, tables, tree expansion, detail panels). This
/// complements the axe scan (<see cref="ConsoleAxeWcagGateTests"/>), which does not
/// verify operable Tab order or focus-indicator appearance — WCAG 2.1.1 (keyboard) and 2.4.7 / 2.4.11
/// (focus visible/appearance) are partly/not axe-automatable (AD2).
/// <para>
/// For each entry route the test drives a real keyboard (genuine <c>Tab</c> traversal, so <c>:focus-visible</c>
/// applies — programmatic <c>focus()</c> would not), collects every console-content interactive control the
/// keyboard reaches, and asserts (a) the keyboard reaches at least one console control (the surface is operable,
/// not a shell-only trap) and (b) every focused console control exposes a visible focus indicator (a non-<c>none</c>
/// computed <c>outline-style</c> or <c>box-shadow</c>). It stays a semantic invariant — never exact coordinates or
/// CSS class names (project AC #13).
/// </para>
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class ConsoleKeyboardFocusGateTests : IClassFixture<AccessibilityConsoleHostFixture>, IAsyncLifetime
{
    // Generous upper bound on Tab presses: the shell chrome exposes only a handful of focusable controls before
    // the console content, so this comfortably traverses into and through the page content on every journey entry.
    private const int MaxTabPresses = 60;

    // Reads document.activeElement after each keyboard Tab and returns a delimited
    // "<inConsole>|<focusVisible>|<id>" string (or "none" for body/root), scoped to the page-content root so
    // shell-chrome focus is distinguished from console-content focus. Delimited (not JSON) to avoid serializer
    // property-name coupling.
    private const string ActiveElementProbe = @"(rootSel) => {
  const el = document.activeElement;
  if (!el || el === document.body || el === document.documentElement) { return 'none'; }
  const inConsole = !!el.closest('[data-testid=""' + rootSel + '""]');
  const s = getComputedStyle(el);
  const hasOutline = !!s.outlineStyle && s.outlineStyle !== 'none';
  const hasShadow = !!s.boxShadow && s.boxShadow !== 'none';
  const id = el.getAttribute('data-testid') || el.tagName.toLowerCase();
  return inConsole + '|' + (hasOutline || hasShadow) + '|' + id;
}";

    private readonly PlaywrightFixture _playwright;
    private readonly AccessibilityConsoleHostFixture _host;
    private IBrowserContext? _context;
    private IPage? _page;

    public ConsoleKeyboardFocusGateTests(PlaywrightFixture playwright, AccessibilityConsoleHostFixture host)
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

    [Theory]
    // Full keyboard-operability / visible-focus coverage across every route on the three critical journeys
    // (UX-DR30: search, filters, result selection, tabs, tables, tree expansion, detail panels). Each route is
    // empirically known to expose at least one keyboard-reachable console-content control (verified during the
    // QA E2E gap pass), so the operability + focus-visible invariants apply to all of them — not just entries.
    [InlineData("folders", "console-page-folders-root", "console-page-folders-root")]                              // J1 entry — discovery (search / filters / result selection)
    [InlineData("workspace", "console-page-workspace-root", "workspace-trust-summary")]                            // J1 terminal — trust summary / matrix tabs
    [InlineData("folder-detail", "console-page-folder-detail-root", "console-page-folder-detail-identity")]        // J1+J2 — metadata tree / detail panels
    [InlineData("provider-support", "console-page-provider-support-root", "console-page-provider-support-matrix")] // J2 — tenant-scoped capability matrix
    [InlineData("provider", "console-page-provider-root", "console-page-provider-section-identity")]              // J2 — folder-scoped provider readiness
    [InlineData("audit-trail", "console-page-audit-trail-root", "console-page-audit-trail-table")]                 // J3 entry — audit table
    [InlineData("operation-timeline", "console-page-operation-timeline-root", "console-page-operation-timeline-table")] // J3 — diagnostic timeline table
    [InlineData("incident-stream", "console-page-incident-stream-root", "console-page-incident-stream-table")]     // J3 — F-6 last-resort read
    public async Task ConsoleJourneyEntry_IsKeyboardOperable_WithVisibleFocus(
        string routeKey,
        string rootTestId,
        string populatedTestId)
    {
        IPage page = Page;

        Uri target = new(_host.BaseAddress, ResolveRoute(routeKey));
        IResponse? response = await page.GotoAsync(target.ToString());
        response.ShouldNotBeNull();
        response.Status.ShouldBeInRange(200, 399);

        await page.Locator($"[data-testid=\"{populatedTestId}\"]")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Genuine keyboard traversal: collect each distinct console-content control the keyboard reaches and
        // whether it shows a visible focus indicator while focused.
        Dictionary<string, bool> consoleControlFocusVisible = [];
        for (int i = 0; i < MaxTabPresses; i++)
        {
            await page.Keyboard.PressAsync("Tab");
            string probe = await page.EvaluateAsync<string>(ActiveElementProbe, rootTestId);
            if (probe == "none")
            {
                continue;
            }

            string[] parts = probe.Split('|', 3);
            bool inConsole = bool.Parse(parts[0]);
            bool focusVisible = bool.Parse(parts[1]);
            string id = parts[2];

            if (inConsole)
            {
                // First time we reach a control records its focus-visibility; keep the strongest signal seen.
                consoleControlFocusVisible[id] = consoleControlFocusVisible.TryGetValue(id, out bool prior)
                    ? prior || focusVisible
                    : focusVisible;
            }
        }

        consoleControlFocusVisible.ShouldNotBeEmpty(
            $"route '{routeKey}' must expose at least one keyboard-reachable console-content control.");

        foreach ((string id, bool focusVisible) in consoleControlFocusVisible)
        {
            focusVisible.ShouldBeTrue(
                string.Create(CultureInfo.InvariantCulture,
                    $"focused console control '{id}' on route '{routeKey}' must show a visible focus indicator (outline or box-shadow)."));
        }
    }

    private static string ResolveRoute(string routeKey) => routeKey switch
    {
        "folders" => ConsoleRoutes.Folders,
        "workspace" => ConsoleRoutes.Workspace(ConsoleStubFixtures.FolderId, ConsoleStubFixtures.WorkspaceId),
        "folder-detail" => ConsoleRoutes.FolderDetail(ConsoleStubFixtures.FolderId),
        "provider-support" => ConsoleRoutes.ProviderSupport,
        "provider" => ConsoleRoutes.Provider(ConsoleStubFixtures.FolderId),
        "audit-trail" => ConsoleRoutes.AuditTrail(ConsoleStubFixtures.FolderId),
        "operation-timeline" => ConsoleRoutes.OperationTimeline(ConsoleStubFixtures.FolderId),
        "incident-stream" => ConsoleRoutes.IncidentStream(ConsoleStubFixtures.FolderId),
        _ => throw new ArgumentOutOfRangeException(nameof(routeKey), routeKey, "Unknown console route key."),
    };

    private IPage Page => _page
        ?? throw new InvalidOperationException("The Playwright page has not completed InitializeAsync.");
}
