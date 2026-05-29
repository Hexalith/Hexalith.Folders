using Bunit;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

// 'Folders' is also the trailing segment of the root namespace (Hexalith.Folders); alias the page type.
using FoldersPage = Hexalith.Folders.UI.Components.Pages.Folders;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.11 / AC #1 / AC #2 / AC #3 — the consolidated no-mutation enforcement sweep: the automated
/// read-only proof for the BDD's seven prohibited paths (UX-DR11 / UX-DR23 / F-2).
/// <para>
/// It renders <b>every</b> console surface — the ten operator pages (Home, Tenants, Folders, FolderDetail,
/// Workspace, AuditTrail, OperationTimeline, Provider, ProviderSupport, IncidentStream) <b>and</b> the two
/// dev-only galleries (RedactionGallery, StateLabelGallery) — in its fully-populated happy-path state and
/// asserts the five-selector command-suppression guard (<see cref="ConsoleTestAssertions"/>) renders empty
/// on each, alongside a single &lt;h1&gt; and the page root. It also proves redacted sentinels never reach
/// the DOM and that no file-browse / raw-diff / file-edit / repair / download affordance is present.
/// </para>
/// <para>
/// The registry-empty structural proof — that the console wires <b>zero</b> domain command manifests — is
/// owned by <see cref="NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest"/> (cited,
/// not duplicated). The contract/telemetry-layer metadata-only backstop is
/// <c>SafetyInvariantGateTests</c> (Hexalith.Folders.Contracts.Tests); it does not cover the rendered
/// console DOM — the DOM assertions in this file are the coverage Story 6.11 adds.
/// </para>
/// </summary>
public sealed class NoMutationConsoleSweepTests
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
    [InlineData("redaction-gallery")]
    [InlineData("state-label-gallery")]
    public void ConsolePage_RendersReadOnly_WithSingleHeading_AndNoMutationAffordances(string page)
    {
        switch (page)
        {
            case "home":
            {
                using BunitContext ctx = BadgeRenderingFixture.Create();
                ConsoleSweepFixtures.AddDevelopmentHostEnvironment(ctx);
                AssertReadOnly(ctx.Render<Home>(), "console-page-home-root");
                break;
            }

            case "tenants":
            {
                using BunitContext ctx = BadgeRenderingFixture.Create();
                AssertReadOnly(ctx.Render<Tenants>(), "console-page-tenants-root");
                break;
            }

            case "folders":
            {
                (BunitContext ctx, _, _) = DiagnosticTestContext.Create();
                using BunitContext _ctx = ctx;
                AssertReadOnly(ctx.Render<FoldersPage>(), "console-page-folders-root");
                break;
            }

            case "folder-detail":
            {
                (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
                using BunitContext _ctx = ctx;
                ConsoleSweepFixtures.StubFolderDetail(client);
                AssertReadOnlyWhenPopulated(
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
                AssertReadOnlyWhenPopulated(
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
                AssertReadOnlyWhenPopulated(
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
                AssertReadOnlyWhenPopulated(
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
                AssertReadOnlyWhenPopulated(
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
                AssertReadOnlyWhenPopulated(
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
                AssertReadOnlyWhenPopulated(
                    ctx.Render<IncidentStream>(),
                    "console-page-incident-stream-root",
                    "console-page-incident-stream-table");
                break;
            }

            case "redaction-gallery":
            {
                using BunitContext ctx = BadgeRenderingFixture.Create();
                AssertReadOnly(ctx.Render<RedactionGallery>(), "console-page-redaction-gallery-root");
                break;
            }

            case "state-label-gallery":
            {
                using BunitContext ctx = BadgeRenderingFixture.Create();
                AssertReadOnly(ctx.Render<StateLabelGallery>(), "console-page-state-label-gallery-root");
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(page), page, "Unknown console page key.");
        }
    }

    [Fact]
    public void RedactedActor_NeverLeaksValue_OnAuditTrail()
    {
        const string sentinel = "SENTINEL-SECRET-ACTOR-6-11";
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;
        ConsoleSweepFixtures.StubAuditTrailWithRedactedActor(client, sentinel);

        IRenderedComponent<AuditTrail> rendered =
            ctx.Render<AuditTrail>(p => p.Add(c => c.FolderId, ConsoleSweepFixtures.FolderId));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-row\"]").ShouldNotBeNull());

        // F-5 / UX-DR11 defence-in-depth: a redacted actor renders the lock affordance, and the value never
        // reaches the DOM even if it leaked onto the wire.
        rendered.FindAll("[data-fc-disclosure=\"redacted\"]").Count.ShouldBeGreaterThanOrEqualTo(1);
        rendered.Markup.ShouldNotContain(sentinel);
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void RedactedWorkspace_NeverLeaksValue_OnOperationTimeline()
    {
        const string sentinel = "SENTINEL-SECRET-WORKSPACE-6-11";
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;
        ConsoleSweepFixtures.StubOperationTimelineWithRedactedWorkspace(client, sentinel);

        IRenderedComponent<OperationTimeline> rendered =
            ctx.Render<OperationTimeline>(p => p.Add(c => c.FolderId, ConsoleSweepFixtures.FolderId));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-row\"]").ShouldNotBeNull());

        rendered.FindAll("[data-fc-disclosure=\"redacted\"]").Count.ShouldBeGreaterThanOrEqualTo(1);
        rendered.Markup.ShouldNotContain(sentinel);
    }

    [Fact]
    public void RedactedWorkspace_NeverLeaksValue_OnIncidentStream_DespiteDegradedMode()
    {
        const string sentinel = "SENTINEL-SECRET-INCIDENT-WS-6-11";
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;
        ConsoleSweepFixtures.StubOperationTimelineWithRedactedWorkspace(client, sentinel);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(ConsoleSweepFixtures.IncidentStreamRoute);

        IRenderedComponent<IncidentStream> rendered = ctx.Render<IncidentStream>();

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-row\"]").ShouldNotBeNull());

        // §3.6 / F-6: incident mode does NOT relax redaction — the persistent degraded banner is present and
        // the redacted value still never reaches the DOM.
        rendered.Find("[data-testid=\"incident-degraded-mode-banner\"]").ShouldNotBeNull();
        rendered.FindAll("[data-fc-disclosure=\"redacted\"]").Count.ShouldBeGreaterThanOrEqualTo(1);
        rendered.Markup.ShouldNotContain(sentinel);
    }

    [Fact]
    public void RedactedCredentialReference_RendersReferenceIdentifierOnly_NeverTheSecret_OnProvider()
    {
        const string sentinel = "SENTINEL-CREDENTIAL-SECRET-6-11";
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;
        ConsoleSweepFixtures.StubProviderWithRedactedCredentialReference(client, sentinel);

        IRenderedComponent<Provider> rendered =
            ctx.Render<Provider>(p => p.Add(c => c.FolderId, ConsoleSweepFixtures.FolderId));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-credential-reference\"]").ShouldNotBeNull());

        // UX-DR12 / FR55: the credential reference is a non-secret reference identifier only; when policy
        // hides it the redacted lock renders and the secret never reaches the DOM.
        rendered.Find("[data-testid=\"console-page-provider-credential-reference\"] [data-fc-disclosure=\"redacted\"]")
            .ShouldNotBeNull();
        rendered.Markup.ShouldContain("reference identifier (not a secret)");
        rendered.Markup.ShouldNotContain(sentinel);
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Theory]
    [InlineData("audit-trail")]
    [InlineData("operation-timeline")]
    [InlineData("provider-support")]
    public void EvidenceTablePage_RendersNoFileBrowse_RawDiff_FileEdit_Repair_OrDownloadAffordance(string page)
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        switch (page)
        {
            case "audit-trail":
                ConsoleSweepFixtures.StubAuditTrail(client);
                IRenderedComponent<AuditTrail> audit =
                    ctx.Render<AuditTrail>(p => p.Add(c => c.FolderId, ConsoleSweepFixtures.FolderId));
                audit.WaitForAssertion(() =>
                    audit.Find("[data-testid=\"console-page-audit-trail-table\"]").ShouldNotBeNull());
                AssertNoFileBrowseDiffEditRepairOrDownloadAffordance(audit);
                break;

            case "operation-timeline":
                ConsoleSweepFixtures.StubOperationTimeline(client);
                IRenderedComponent<OperationTimeline> timeline =
                    ctx.Render<OperationTimeline>(p => p.Add(c => c.FolderId, ConsoleSweepFixtures.FolderId));
                timeline.WaitForAssertion(() =>
                    timeline.Find("[data-testid=\"console-page-operation-timeline-table\"]").ShouldNotBeNull());
                AssertNoFileBrowseDiffEditRepairOrDownloadAffordance(timeline);
                break;

            default:
                ConsoleSweepFixtures.StubProviderSupport(client);
                IRenderedComponent<ProviderSupport> support = ctx.Render<ProviderSupport>();
                support.WaitForAssertion(() =>
                    support.Find("[data-testid=\"console-page-provider-support-matrix\"]").ShouldNotBeNull());
                AssertNoFileBrowseDiffEditRepairOrDownloadAffordance(support);
                break;
        }
    }

    private static void AssertNoFileBrowseDiffEditRepairOrDownloadAffordance<T>(IRenderedComponent<T> rendered)
        where T : IComponent
    {
        // UX-DR11 / architecture.md L91: no file-content browsing (no file input), no file editing
        // (no textarea), no raw-diff/repair/download affordance (no downloadable anchor), no unrestricted
        // filesystem browse. The structural command-suppression guard backs this for mutation paths.
        rendered.FindAll("a[download]").ShouldBeEmpty();
        rendered.FindAll("textarea").ShouldBeEmpty();
        rendered.FindAll("input[type=\"file\"]").ShouldBeEmpty();
    }

    private static void AssertReadOnly<T>(IRenderedComponent<T> rendered, string rootTestId)
        where T : IComponent
    {
        rendered.Find($"[data-testid=\"{rootTestId}\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
        rendered.ShouldHaveNoMutationAffordances();
    }

    private static void AssertReadOnlyWhenPopulated<T>(IRenderedComponent<T> rendered, string rootTestId, string populatedTestId)
        where T : IComponent
    {
        rendered.WaitForAssertion(() =>
            rendered.Find($"[data-testid=\"{populatedTestId}\"]").ShouldNotBeNull());
        AssertReadOnly(rendered, rootTestId);
    }
}
