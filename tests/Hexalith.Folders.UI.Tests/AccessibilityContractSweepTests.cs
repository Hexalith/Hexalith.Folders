using System.IO;
using System.Linq;

using AngleSharp.Dom;

using Bunit;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components;
using Hexalith.Folders.UI.Components.Models;
using Hexalith.Folders.UI.Components.Pages;
using Hexalith.Folders.UI.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

// 'Folders' is also the trailing segment of the root namespace (Hexalith.Folders); alias the page type.
using FoldersPage = Hexalith.Folders.UI.Components.Pages.Folders;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.11 / AC #4 / AC #5 / AC #11 — the consolidated WCAG 2.2 AA <b>structural</b> sweep (UX-DR30) plus
/// the non-color-only / redaction-distinction sweep (UX-DR14 / UX-DR10 / UX-DR22 / F-4 / F-5).
/// <para>
/// Across every operator page it asserts the structural invariants uniformly: exactly one <c>&lt;h1&gt;</c>;
/// every <c>&lt;table&gt;</c> carries a <c>&lt;caption&gt;</c> and <c>scope</c>-attributed header cells; every
/// <c>&lt;nav&gt;</c> carries an <c>aria-label</c>; every interactive control (<c>&lt;button&gt;</c>,
/// <c>&lt;a&gt;</c>, <c>&lt;input&gt;</c>) is a keyboard-reachable native element with an accessible name; and
/// no mutation affordance is present. The app-shell facts (<c>&lt;html lang="en"&gt;</c> + responsive viewport
/// meta in <c>App.razor</c>, and <c>FocusOnNavigate Selector="h1"</c> in <c>Routes.razor</c>) are asserted
/// against the source contract since they render in the host document, not an isolated bUnit page render.
/// </para>
/// <para>
/// The dev-only galleries (RedactionGallery / StateLabelGallery) render through <c>FluentDataGrid</c> (Fluent
/// UI, F-3 — the WCAG 2.2 AA foundation) rather than hand-authored semantic tables, so they are verified for
/// no-mutation + heading structure by <see cref="NoMutationConsoleSweepTests"/> and inherit Fluent UI's
/// grid accessibility; they are intentionally out of scope for the hand-authored-markup structural sweep.
/// </para>
/// </summary>
public sealed class AccessibilityContractSweepTests
{
    [Theory]
    [InlineData("home")]
    [InlineData("tenants")]
    [InlineData("folders")]
    [InlineData("folder-detail")]
    [InlineData("workspace")]
    [InlineData("audit-trail")]
    [InlineData("operation-timeline")]
    [InlineData("provider")]
    [InlineData("provider-support")]
    [InlineData("incident-stream")]
    public void OperatorPage_SatisfiesWcag22AaStructuralContract(string page)
    {
        switch (page)
        {
            case "home":
                {
                    using BunitContext ctx = BadgeRenderingFixture.Create();
                    ConsoleSweepFixtures.AddDevelopmentHostEnvironment(ctx);
                    AssertStructuralA11y(ctx.Render<Home>(), "console-page-home-root");
                    break;
                }

            case "tenants":
                {
                    using BunitContext ctx = BadgeRenderingFixture.Create();
                    AssertStructuralA11y(ctx.Render<Tenants>(), "console-page-tenants-root");
                    break;
                }

            case "folders":
                {
                    (BunitContext ctx, _, _) = DiagnosticTestContext.Create();
                    using BunitContext _ctx = ctx;
                    AssertStructuralA11y(ctx.Render<FoldersPage>(), "console-page-folders-root");
                    break;
                }

            case "folder-detail":
                {
                    (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
                    using BunitContext _ctx = ctx;
                    ConsoleSweepFixtures.StubFolderDetail(client);
                    AssertStructuralA11yWhenPopulated(
                        ctx.Render<FolderDetail>(p => p.Add(d => d.FolderId, ConsoleSweepFixtures.FolderId)),
                        "console-page-folder-detail-root",
                        "console-page-folder-detail-identity");
                    break;
                }

            case "workspace":
                {
                    (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
                    using BunitContext _ctx = ctx;
                    ConsoleSweepFixtures.StubWorkspace(client);
                    AssertStructuralA11yWhenPopulated(
                        ctx.Render<Workspace>(p => p
                            .Add(w => w.FolderId, ConsoleSweepFixtures.FolderId)
                            .Add(w => w.WorkspaceId, ConsoleSweepFixtures.WorkspaceId)),
                        "console-page-workspace-root",
                        "workspace-trust-summary");
                    break;
                }

            case "audit-trail":
                {
                    (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
                    using BunitContext _ctx = ctx;
                    ConsoleSweepFixtures.StubAuditTrail(client);
                    AssertStructuralA11yWhenPopulated(
                        ctx.Render<AuditTrail>(p => p.Add(c => c.FolderId, ConsoleSweepFixtures.FolderId)),
                        "console-page-audit-trail-root",
                        "console-page-audit-trail-table");
                    break;
                }

            case "operation-timeline":
                {
                    (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
                    using BunitContext _ctx = ctx;
                    ConsoleSweepFixtures.StubOperationTimeline(client);
                    AssertStructuralA11yWhenPopulated(
                        ctx.Render<OperationTimeline>(p => p.Add(c => c.FolderId, ConsoleSweepFixtures.FolderId)),
                        "console-page-operation-timeline-root",
                        "console-page-operation-timeline-table");
                    break;
                }

            case "provider":
                {
                    (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
                    using BunitContext _ctx = ctx;
                    ConsoleSweepFixtures.StubProvider(client);
                    AssertStructuralA11yWhenPopulated(
                        ctx.Render<Provider>(p => p.Add(c => c.FolderId, ConsoleSweepFixtures.FolderId)),
                        "console-page-provider-root",
                        "console-page-provider-section-identity");
                    break;
                }

            case "provider-support":
                {
                    (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
                    using BunitContext _ctx = ctx;
                    ConsoleSweepFixtures.StubProviderSupport(client);
                    AssertStructuralA11yWhenPopulated(
                        ctx.Render<ProviderSupport>(),
                        "console-page-provider-support-root",
                        "console-page-provider-support-matrix");
                    break;
                }

            case "incident-stream":
                {
                    (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
                    using BunitContext _ctx = ctx;
                    ConsoleSweepFixtures.StubIncidentStream(client);
                    ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(ConsoleSweepFixtures.IncidentStreamRoute);
                    AssertStructuralA11yWhenPopulated(
                        ctx.Render<IncidentStream>(),
                        "console-page-incident-stream-root",
                        "console-page-incident-stream-table");
                    break;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(page), page, "Unknown operator page key.");
        }
    }

    [Fact]
    public void AppShell_DeclaresLangEn_ResponsiveViewport_AndFocusOnNavigateToH1()
    {
        // UX-DR30 / UX-DR31: <html lang="en"> + a responsive viewport meta render in the host document
        // (App.razor), and FocusOnNavigate lands focus on the page <h1> after navigation (Routes.razor).
        // These are host-document/router facts, not isolated-page render facts, so they are verified against
        // the source contract.
        string app = ReadUiSource("Components/App.razor");
        app.ShouldContain("<html lang=\"en\">");
        app.ShouldContain("name=\"viewport\"");
        app.ShouldContain("width=device-width");

        string routes = ReadUiSource("Components/Routes.razor");
        routes.ShouldContain("FocusOnNavigate");
        routes.ShouldContain("Selector=\"h1\"");
    }

    [Fact]
    public void RedactedField_FourStates_DistinctTokens_LockOnlyOnRedacted_AndNeverLeaksValue()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        // UX-DR10 / UX-DR22 / F-5: the four disclosure states render distinct data-fc-disclosure tokens,
        // the lock icon appears ONLY on redacted, the redacted value never leaks, and every non-visible
        // state carries a screen-reader-meaningful aria-label (non-color-only).
        string[] tokens = Enum.GetValues<FieldDisclosure>()
            .Select(d => Token(Render(ctx, d, value: d == FieldDisclosure.Visible ? "acme/widgets" : null)))
            .ToArray();
        tokens.Distinct(StringComparer.Ordinal).Count().ShouldBe(Enum.GetValues<FieldDisclosure>().Length);

        Render(ctx, FieldDisclosure.Redacted, value: "SENTINEL-LEAK-6-11").Markup.ShouldNotContain("SENTINEL-LEAK-6-11");
        Render(ctx, FieldDisclosure.Redacted).FindAll("svg").Count.ShouldBeGreaterThanOrEqualTo(1);
        Render(ctx, FieldDisclosure.Unknown).FindAll("svg").ShouldBeEmpty();
        Render(ctx, FieldDisclosure.Missing).FindAll("svg").ShouldBeEmpty();

        foreach (FieldDisclosure disclosure in new[] { FieldDisclosure.Redacted, FieldDisclosure.Unknown, FieldDisclosure.Missing })
        {
            IRenderedComponent<RedactedField> rendered = ctx.Render<RedactedField>(p => p
                .Add(f => f.Disclosure, disclosure)
                .Add(f => f.ColumnHeader, "Branch"));
            rendered.Find("[data-testid=\"redacted-field\"]").GetAttribute("aria-label").ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void OperatorDispositionBadge_EveryDisposition_RendersTextLabel_Slot_AndAriaLabel_NotColorAlone()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        // UX-DR14 / F-4: every operator disposition conveys via a visible text label + a non-color slot token
        // + an accessible aria-label — never colour alone.
        foreach (OperatorDispositionLabel disposition in Enum.GetValues<OperatorDispositionLabel>())
        {
            IRenderedComponent<OperatorDispositionBadge> rendered = ctx.Render<OperatorDispositionBadge>(p => p
                .Add(b => b.Disposition, disposition)
                .Add(b => b.ColumnHeader, "Status"));

            // Visible text label (the non-color channel), the slot token (shape), and the aria-label.
            rendered.Find("[data-testid=\"operator-disposition-badge\"]").TextContent.ShouldNotBeNullOrWhiteSpace();
            rendered.Find("[data-fc-badge-slot]").GetAttribute("data-fc-badge-slot").ShouldNotBeNullOrWhiteSpace();
            rendered.Find("[aria-label]").GetAttribute("aria-label").ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void TrustMatrix_EveryDimensionState_RendersIconAndLabel_NotColorAlone()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        // UX-DR14 / F-4: every trust-dimension state conveys via icon (shape) + a resolved text label.
        foreach (TrustDimensionState state in Enum.GetValues<TrustDimensionState>())
        {
            IReadOnlyList<TrustMatrixCell> cells =
            [
                new("Dimension", state, "reason", DateTimeOffset.UnixEpoch, null, null),
            ];
            IRenderedComponent<TrustMatrix> rendered = ctx.Render<TrustMatrix>(p => p.Add(m => m.Cells, cells));

            IElement cell = rendered.Find("[data-testid=\"trust-matrix-cell\"]");
            cell.QuerySelectorAll("svg").Length.ShouldBeGreaterThan(0);
            cell.TextContent.ShouldContain(TrustDimensionStateMapper.ResolveLabel(state));
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // Structural assertion helpers.
    // ---------------------------------------------------------------------------------------------------

    private static void AssertStructuralA11yWhenPopulated<T>(IRenderedComponent<T> rendered, string rootTestId, string populatedTestId)
        where T : IComponent
    {
        rendered.WaitForAssertion(() =>
            rendered.Find($"[data-testid=\"{populatedTestId}\"]").ShouldNotBeNull());
        AssertStructuralA11y(rendered, rootTestId);
    }

    private static void AssertStructuralA11y<T>(IRenderedComponent<T> rendered, string rootTestId)
        where T : IComponent
    {
        // Semantic heading + page root (UX-DR30 semantic headings).
        rendered.Find($"[data-testid=\"{rootTestId}\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);

        // Readable tables: a caption and scope-attributed header cells (UX-DR30).
        foreach (IElement table in rendered.FindAll("table"))
        {
            table.QuerySelector("caption").ShouldNotBeNull();
            IElement[] headers = table.QuerySelectorAll("th").ToArray();
            headers.Length.ShouldBeGreaterThan(0);
            foreach (IElement th in headers)
            {
                th.GetAttribute("scope").ShouldNotBeNullOrWhiteSpace();
            }
        }

        // Pagination / landmark navs carry an accessible name (UX-DR30).
        foreach (IElement nav in rendered.FindAll("nav"))
        {
            nav.GetAttribute("aria-label").ShouldNotBeNullOrWhiteSpace();
        }

        // Every interactive control is a keyboard-reachable native element with an accessible name
        // (UX-DR30 keyboard reachability + non-color-only naming; UX-DR24 focusable controls).
        foreach (IElement button in rendered.FindAll("button"))
        {
            HasAccessibleName(button).ShouldBeTrue($"<button> lacks an accessible name: {button.OuterHtml}");
        }

        foreach (IElement anchor in rendered.FindAll("a"))
        {
            anchor.GetAttribute("href").ShouldNotBeNullOrWhiteSpace();
            HasAccessibleName(anchor).ShouldBeTrue($"<a> lacks an accessible name: {anchor.OuterHtml}");
        }

        foreach (IElement input in rendered.FindAll("input"))
        {
            InputHasAccessibleName(rendered, input).ShouldBeTrue($"<input> lacks an accessible name: {input.OuterHtml}");
        }

        rendered.ShouldHaveNoMutationAffordances();
    }

    private static bool HasAccessibleName(IElement element)
        => !string.IsNullOrWhiteSpace(element.TextContent)
        || !string.IsNullOrWhiteSpace(element.GetAttribute("aria-label"))
        || !string.IsNullOrWhiteSpace(element.GetAttribute("title"));

    private static bool InputHasAccessibleName<T>(IRenderedComponent<T> rendered, IElement input)
        where T : IComponent
    {
        if (!string.IsNullOrWhiteSpace(input.GetAttribute("aria-label")))
        {
            return true;
        }

        string? id = input.GetAttribute("id");
        return !string.IsNullOrWhiteSpace(id) && rendered.FindAll($"label[for=\"{id}\"]").Count > 0;
    }

    private static IRenderedComponent<RedactedField> Render(BunitContext ctx, FieldDisclosure disclosure, string? value = null)
        => ctx.Render<RedactedField>(p =>
        {
            p.Add(f => f.Disclosure, disclosure);
            if (value is not null)
            {
                p.Add(f => f.Value, value);
            }
        });

    private static string Token(IRenderedComponent<RedactedField> rendered)
        => rendered.Find("[data-testid=\"redacted-field\"]").GetAttribute("data-fc-disclosure") ?? string.Empty;

    private static string ReadUiSource(string relativePath)
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Hexalith.Folders.slnx")))
        {
            dir = dir.Parent;
        }

        dir.ShouldNotBeNull("Could not locate the repository root (Hexalith.Folders.slnx) from the test base directory.");
        string path = Path.Combine(dir!.FullName, "src", "Hexalith.Folders.UI", relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).ShouldBeTrue($"Expected UI source file not found: {path}");
        return File.ReadAllText(path);
    }
}
