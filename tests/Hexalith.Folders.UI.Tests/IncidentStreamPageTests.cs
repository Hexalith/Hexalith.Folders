using System;
using System.Collections.Generic;
using System.Net.Http;

using Bunit;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components;
using Hexalith.Folders.UI.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using Xunit;

// xUnit1051 fires on NSubstitute arg-matcher setups for IClient methods that have a CancellationToken
// overload; these are substitute configuration (matching the no-token overload the page calls), not
// cancellable operations, so the rule does not apply here.
#pragma warning disable xUnit1051

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.9 / F-6 — the incident-mode last-resort read path (wireflow §3.5). The page renders the three
/// F-6 guardrails on top of the existing folder-scoped operation-timeline read: (1) a PERSISTENT
/// degraded-mode banner present in every branch, (2) the operator disposition BESIDE the raw technical
/// state transition, and (3) a one-click correlation + time-window copy affordance. Redaction does not
/// relax while degraded (§3.6 / F-5); tenant scope is server-sourced; the incident-permission decision is
/// the server's (safe denial); and the page stays strictly read-only and metadata-only.
/// </summary>
public sealed class IncidentStreamPageTests
{
    private const string Route = "/_admin/incident-stream?folder=folder-1";

    private static readonly IReadOnlyDictionary<string, IEnumerable<string>> EmptyHeaders =
        new Dictionary<string, IEnumerable<string>>();

    [Fact]
    public void RendersIncidentStream_WithFullFieldSet_BannerAndScopeBeforeHeading()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-table\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-incident-stream-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);

        // Scope banner is ordered before the persistent degraded banner, both before <h1> (scope-before-evidence).
        int scopeIndex = rendered.Markup.IndexOf("tenant-scope-banner", StringComparison.Ordinal);
        int bannerIndex = rendered.Markup.IndexOf("incident-degraded-mode-banner", StringComparison.Ordinal);
        int headingIndex = rendered.Markup.IndexOf("<h1", StringComparison.Ordinal);
        scopeIndex.ShouldBeLessThan(headingIndex);
        bannerIndex.ShouldBeLessThan(headingIndex);

        rendered.FindAll("[data-testid=\"console-page-incident-stream-row\"]").Count.ShouldBe(1);
        rendered.Find("[data-testid=\"console-page-incident-stream-entry-id\"]").TextContent.ShouldBe("entry-1");
        // AC#3 raw-event field set: the evidence timestamp is one of the named surfaced fields — assert the
        // populated cell renders the formatted "u" value (UnixEpoch → 1970-01-01 00:00:00Z), not just that a
        // slot exists, so a regression dropping the timestamp column would be caught.
        rendered.Find("[data-testid=\"console-page-incident-stream-timestamp\"]").TextContent.ShouldContain("1970-01-01");
        // A populated operation/task/correlation identifier renders (the missing-ids test covers the null path).
        rendered.Markup.ShouldContain("op-1");
        rendered.Find("[data-testid=\"console-page-incident-stream-result\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"console-page-incident-stream-retryable\"]").TextContent.ShouldBe("No");
        rendered.Find("[data-testid=\"console-page-incident-stream-duration\"]").TextContent.ShouldContain("99");

        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void DefaultEvidenceTimestamp_RendersUnknown_NotFabricated()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // AC#3 / UX-DR26 freshness honesty (the recurring 6.7/6.8 review fix, mirrored from the template's
        // DefaultTimestamp_RendersUnknown_NotFabricated): a default/min evidence timestamp renders "unknown"
        // in the row's timestamp cell — never a fabricated 0001-01-01.
        OperationTimelineEntry entry = VisibleEntry();
        entry.EvidenceTimestamp = default;
        StubList(client, Page(truncated: false, cursor: null, entry));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-timestamp\"]").ShouldNotBeNull());

        string timestamp = rendered.Find("[data-testid=\"console-page-incident-stream-timestamp\"]").TextContent;
        timestamp.ShouldContain("unknown");
        timestamp.ShouldNotContain("0001");
    }

    [Fact]
    public void DegradedModeBanner_IsAlwaysPresent_AndCorrelationCopy_InAuthorizedBranch()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-table\"]").ShouldNotBeNull());

        // F-6 guardrail 1 + guardrail 3 are present on the authorized read.
        rendered.Find("[data-testid=\"incident-degraded-mode-banner\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"incident-correlation-copy\"]").ShouldNotBeNull();
    }

    [Fact]
    public void Disposition_RendersBesideRawTransition_UsingServerValue_NotDerivedFromToState()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // ToState = Ready would DERIVE "Available"; the server-computed disposition must win (F-4 / guardrail 2).
        OperationTimelineEntry entry = VisibleEntry();
        entry.StateTransition = new DiagnosticStateTransition
        {
            FromState = LifecycleState.Preparing,
            ToState = LifecycleState.Ready,
            Disposition = OperatorDispositionLabel.Awaiting_human,
        };
        StubList(client, Page(truncated: false, cursor: null, entry));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-transition\"]").ShouldNotBeNull());

        // The disposition badge sits BESIDE the raw technical transition and carries the server value.
        rendered.Find("[data-testid=\"console-page-incident-stream-transition\"] [data-testid=\"operator-disposition-badge\"]")
            .TextContent.ShouldContain("Awaiting human");
        rendered.FindAll("[data-testid=\"console-page-incident-stream-transition\"] [data-fc-technical-state]")
            .Count.ShouldBe(2);
    }

    [Fact]
    public void RedactedWorkspaceReference_RendersRedacted_AndValueNotEmitted()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // §3.6 / F-5: incident mode does NOT relax redaction — a redacted value never reaches the DOM.
        OperationTimelineEntry entry = VisibleEntry();
        entry.WorkspaceReference = Workspace("SECRET-WORKSPACE", RedactionMetadataVisibility.Redacted);
        StubList(client, Page(truncated: false, cursor: null, entry));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-row\"]").ShouldNotBeNull());

        rendered.FindAll("[data-fc-disclosure=\"redacted\"]").Count.ShouldBeGreaterThanOrEqualTo(1);
        rendered.Markup.ShouldNotContain("SECRET-WORKSPACE");
    }

    [Fact]
    public void MissingIdentifiers_RenderDistinctMissingAffordances_NotRedacted()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        OperationTimelineEntry entry = VisibleEntry();
        entry.TimelineEntryId = null;
        entry.OperationId = null;
        entry.TaskId = null;
        entry.CorrelationId = null;
        StubList(client, Page(truncated: false, cursor: null, entry));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-row\"]").ShouldNotBeNull());

        // Missing ≠ Redacted distinctness (AC #6): the four absent ids each report an honest Missing affordance.
        rendered.FindAll("[data-testid=\"console-page-incident-stream-row\"] [data-fc-disclosure=\"missing\"]")
            .Count.ShouldBe(4);
        rendered.FindAll("[data-testid=\"console-page-incident-stream-entry-id\"]").ShouldBeEmpty();
    }

    [Fact]
    public void AbsentFreshness_RendersCheckpointUnknown_NotFabricated()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        OperationTimelinePage page = Page(truncated: false, cursor: null, VisibleEntry());
        page.Freshness = null!;
        StubList(client, page);

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"incident-degraded-mode-checkpoint\"]").ShouldNotBeNull());

        // AC #2 / UX-DR26: absent freshness renders "unknown" — never a fabricated 0001-01-01, never "Current".
        string checkpoint = rendered.Find("[data-testid=\"incident-degraded-mode-checkpoint\"]").TextContent;
        checkpoint.ShouldContain("unknown");
        checkpoint.ShouldNotContain("0001");
        checkpoint.ShouldNotContain("Current");
    }

    [Fact]
    public void TruncatedPage_RendersNextLink_PreservingCursorAndFolder()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: true, cursor: "next-cursor", VisibleEntry()));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-next\"]").ShouldNotBeNull());

        string? href = rendered.Find("[data-testid=\"console-page-incident-stream-next\"]").GetAttribute("href");
        href.ShouldNotBeNull();
        href!.ShouldContain("cursor=next-cursor");
        href.ShouldContain("folder=folder-1");
    }

    [Fact]
    public void NonTruncatedPage_RendersEndOfResults()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-end\"]").ShouldNotBeNull());

        rendered.FindAll("[data-testid=\"console-page-incident-stream-next\"]").ShouldBeEmpty();
    }

    [Fact]
    public void Folderless_RendersInstructionState_AttemptsNoRead_BannerStillPresent()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        // No ?folder= supplied → render at the bare route.
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/_admin/incident-stream");
        IRenderedComponent<IncidentStream> rendered = ctx.Render<IncidentStream>();

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-fc-empty-reason=\"insufficient_filter_scope\"]").ShouldNotBeNull());

        // AC #9a: an instruction/empty state — never an error, never a fabricated event list, no read attempted.
        rendered.FindAll("[data-testid=\"console-page-incident-stream-table\"]").ShouldBeEmpty();
        rendered.FindAll("[data-testid=\"console-error-panel\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"console-page-incident-stream-root\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"incident-degraded-mode-banner\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
        client.DidNotReceive().ListOperationTimelineAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
            Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>());
    }

    [Fact]
    public void EmptyEntries_RendersNoMatches_BannerStillPresent()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-fc-empty-reason=\"no_matches\"]").ShouldNotBeNull());

        rendered.FindAll("[data-testid=\"console-page-incident-stream-table\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"incident-degraded-mode-banner\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
    }

    [Fact]
    public void DeniedRead_RendersSafeDenial_WithoutTable_BannerStillPresent()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // AC #8: the incident-permission/ACL decision is the server's — surface the canonical token only.
        const string body = """{"category":"audit_access_denied","correlationId":"corr-y","retryable":false}""";
        client.ListOperationTimelineAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new HexalithFoldersApiException("denied", 403, body, EmptyHeaders, innerException: null));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-error-panel\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-error-category\"]").TextContent.ShouldBe("audit_access_denied");
        rendered.FindAll("[data-testid=\"console-page-incident-stream-table\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"incident-degraded-mode-banner\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void TransportFailure_RendersReadModelUnavailable_BannerStillPresent()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.ListOperationTimelineAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new HttpRequestException("connection refused"));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-fc-empty-reason=\"read_model_unavailable\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-incident-stream-root\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"incident-degraded-mode-banner\"]").ShouldNotBeNull();
        rendered.FindAll("[data-testid=\"console-page-incident-stream-table\"]").ShouldBeEmpty();
        rendered.FindAll("h1").Count.ShouldBe(1);
    }

    [Fact]
    public void ReadCancellation_RendersReadModelUnavailable()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.ListOperationTimelineAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new TaskCanceledException());

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-fc-empty-reason=\"read_model_unavailable\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"incident-degraded-mode-banner\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
    }

    [Fact]
    public void IncomingFolderAndCursorQueryParams_ReachRead_WithNullFilter()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        ctx.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/_admin/incident-stream?folder=folder-1&cursor=cur-1");
        IRenderedComponent<IncidentStream> rendered = ctx.Render<IncidentStream>();

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-table\"]").ShouldNotBeNull());

        // The folder + cursor query params drive the read; filter is always null (C4 rejection-only).
        // Arg.Is<string>(f => f == null) avoids the NSubstitute AmbiguousArgumentsException (6.8 fix).
        client.Received(1).ListOperationTimelineAsync(
            "folder-1", Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
            Arg.Is<string>(c => c == "cur-1"), Arg.Any<int?>(), Arg.Is<string>(f => f == null));

        rendered.FindAll("[data-testid=\"console-page-incident-stream-row\"]").Count.ShouldBe(1);
    }

    [Fact]
    public void IncidentStream_RendersNoFilterControl()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-table\"]").ShouldNotBeNull());

        // AC #11: "filtered" is a server capability only — render no filter control.
        rendered.FindAll("input").ShouldBeEmpty();
        rendered.FindAll("select").ShouldBeEmpty();
        rendered.FindAll("textarea").ShouldBeEmpty();
        rendered.FindAll("[type=\"search\"]").ShouldBeEmpty();
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void TenantScope_RendersAccessorTenant_NotFolderQuery()
    {
        // Accessor tenant deliberately differs from the ?folder= query value.
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create(tenantId: "tenant-zeta");
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        // AC #7: tenant scope is sourced from IUserContextAccessor.TenantId, never the ?folder= query.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"tenant-scope-tenant-id\"]").ShouldNotBeNull());
        string tenant = rendered.Find("[data-testid=\"tenant-scope-tenant-id\"]").TextContent;
        tenant.ShouldBe("tenant-zeta");
        tenant.ShouldNotBe("folder-1");
    }

    [Fact]
    public void RendersBackLinks_ToOperationTimelineAndAuditTrail_WithFolderScopedHrefs()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        // AC #13 / UX-DR19: connected evidence — the incident page cross-links back to the folder's
        // authoritative diagnostic views, so the last-resort view is not isolated.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-operation-timeline-link\"]").ShouldNotBeNull());
        rendered.Find("[data-testid=\"console-page-incident-stream-operation-timeline-link\"]")
            .GetAttribute("href").ShouldBe("/folders/folder-1/operation-timeline");
        rendered.Find("[data-testid=\"console-page-incident-stream-audit-trail-link\"]")
            .GetAttribute("href").ShouldBe("/folders/folder-1/audit-trail");
    }

    [Fact]
    public void WhilePrimaryReadPending_RendersLoadingBusyIndicator_BannerStillPresent()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // AC #14: F-7 perceived-wait (skeleton/cancel) is Story 6.10 — here the page renders ONLY a simple
        // labelled aria-busy paragraph while the primary read is pending, with no table, and the PERSISTENT
        // degraded banner + root + single <h1> still present.
        TaskCompletionSource<OperationTimelinePage> pending = new();
        client.ListOperationTimelineAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .Returns(pending.Task);

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-loading\"]").ShouldNotBeNull());
        rendered.Find("[data-testid=\"console-page-incident-stream-loading\"]").GetAttribute("aria-busy").ShouldBe("true");
        rendered.FindAll("[data-testid=\"console-page-incident-stream-table\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"console-page-incident-stream-root\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"incident-degraded-mode-banner\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);

        // Resolving the read transitions off the loading branch into the rendered table.
        pending.SetResult(Page(truncated: false, cursor: null, VisibleEntry()));
        rendered.WaitForAssertion(() =>
        {
            rendered.Find("[data-testid=\"console-page-incident-stream-table\"]").ShouldNotBeNull();
            rendered.FindAll("[data-testid=\"console-page-incident-stream-loading\"]").ShouldBeEmpty();
        });
    }

    [Fact]
    public void StaleFreshness_RendersStaleInFooter_NotCurrent()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // AC #2 / UX-DR26 freshness honesty (the recurring 6.7/6.8 review fix): a flagged-stale projection
        // renders "Stale" in the evidence-freshness footer — never "Current".
        OperationTimelinePage page = Page(truncated: false, cursor: null, VisibleEntry());
        page.Freshness = new FreshnessMetadata
        {
            Stale = true,
            ObservedAt = DateTimeOffset.UnixEpoch,
            ProjectionWatermark = "wm-1",
            ReadConsistency = ReadConsistencyClass.Eventually_consistent,
        };
        StubList(client, page);

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-freshness\"]").ShouldNotBeNull());

        string freshness = rendered.Find("[data-testid=\"console-page-incident-stream-freshness\"]").TextContent;
        freshness.ShouldContain("Stale");
        freshness.ShouldNotContain("Current");
    }

    [Fact]
    public void PresentFreshness_RendersRealCheckpointInBanner_NotUnknown()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // AC #2: the happy-path complement to the absent-freshness test — a present observed-at renders the
        // real UTC checkpoint in the persistent banner, never the "unknown" honesty fallback.
        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"incident-degraded-mode-checkpoint\"]").ShouldNotBeNull());

        string checkpoint = rendered.Find("[data-testid=\"incident-degraded-mode-checkpoint\"]").TextContent;
        checkpoint.ShouldContain("1970-01-01 00:00:00Z");
        checkpoint.ShouldNotContain("unknown");
        checkpoint.ShouldNotContain("0001");
    }

    [Fact]
    public void MultipleEntries_RenderOneRowEach_NoClientSideHiding()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // AC #10: no client-side filtering/hiding of returned rows — every entry the projection returns is
        // rendered as its own incident row.
        OperationTimelineEntry e1 = VisibleEntry();
        e1.TimelineEntryId = "entry-1";
        OperationTimelineEntry e2 = VisibleEntry();
        e2.TimelineEntryId = "entry-2";
        OperationTimelineEntry e3 = VisibleEntry();
        e3.TimelineEntryId = "entry-3";
        StubList(client, Page(truncated: false, cursor: null, e1, e2, e3));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.FindAll("[data-testid=\"console-page-incident-stream-row\"]").Count.ShouldBe(3));

        List<string> ids = rendered
            .FindAll("[data-testid=\"console-page-incident-stream-entry-id\"]")
            .Select(e => e.TextContent)
            .ToList();
        ids.Count.ShouldBe(3);
        ids[0].ShouldContain("entry-1");
        ids[1].ShouldContain("entry-2");
        ids[2].ShouldContain("entry-3");
    }

    [Fact]
    public void CorrelationCopyButton_ReceivesOldestToNewestTimeWindow_FromVisibleEntries()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // AC #5: the copy affordance's metadata-only payload is the page correlation id + the UTC timestamp
        // WINDOW of the currently-shown events (oldest..newest EvidenceTimestamp). Assert the page computes
        // that window and feeds it to the button (the component test already proves the composed string).
        OperationTimelineEntry oldest = VisibleEntry();
        oldest.EvidenceTimestamp = DateTimeOffset.UnixEpoch;
        OperationTimelineEntry middle = VisibleEntry();
        middle.EvidenceTimestamp = DateTimeOffset.UnixEpoch.AddHours(1);
        OperationTimelineEntry newest = VisibleEntry();
        newest.EvidenceTimestamp = DateTimeOffset.UnixEpoch.AddHours(2);
        StubList(client, Page(truncated: false, cursor: null, newest, middle, oldest));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-table\"]").ShouldNotBeNull());

        CorrelationCopyButton copy = rendered.FindComponent<CorrelationCopyButton>().Instance;
        copy.CorrelationId.ShouldNotBeNullOrWhiteSpace();
        copy.TimeWindow.ShouldNotBeNull();
        copy.TimeWindow!.ShouldBe("1970-01-01 00:00:00Z..1970-01-01 02:00:00Z");
    }

    [Fact]
    public void BlankWorkspaceMetadataOnly_RendersMissing_DistinctFromRedacted()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // AC #6: Redacted ≠ Missing must stay visibly distinct. A blank workspace value with metadata_only
        // visibility resolves to Missing — never Redacted (no lock icon, no fabricated value).
        OperationTimelineEntry entry = VisibleEntry();
        entry.WorkspaceReference = Workspace(null);
        StubList(client, Page(truncated: false, cursor: null, entry));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-row\"]").ShouldNotBeNull());

        rendered.FindAll("[data-fc-disclosure=\"missing\"]").Count.ShouldBeGreaterThanOrEqualTo(1);
        rendered.FindAll("[data-fc-disclosure=\"redacted\"]").ShouldBeEmpty();
    }

    [Fact]
    public void SupplementaryPermissionsDenied_IsSwallowed_TableStillRenders()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // AC #7: the supplementary effective-permissions read uses the swallow-denial TryReadAsync helper.
        // A denial on THAT read must not block the page — the authoritative gate is the primary timeline read,
        // which here succeeds, so the event table still renders (no error panel from the swallowed denial).
        client.GetEffectivePermissionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>())
            .ThrowsAsync(new HexalithFoldersApiException("denied", 403, "{}", EmptyHeaders, innerException: null));
        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        IRenderedComponent<IncidentStream> rendered = RenderForFolder(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-incident-stream-table\"]").ShouldNotBeNull());

        rendered.FindAll("[data-testid=\"console-error-panel\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"incident-degraded-mode-banner\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
    }

    private static IRenderedComponent<IncidentStream> RenderForFolder(BunitContext ctx)
    {
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(Route);
        return ctx.Render<IncidentStream>();
    }

    private static void StubList(IClient client, OperationTimelinePage page)
        => client.ListOperationTimelineAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .Returns(page);

    private static OperationTimelinePage Page(bool truncated, string? cursor, params OperationTimelineEntry[] entries)
        => new()
        {
            Entries = [.. entries],
            Page = new PaginationMetadata { Cursor = cursor ?? string.Empty, Limit = 50, IsTruncated = truncated },
            RetentionClass = "TODO(reference-pending):default",
            Freshness = new FreshnessMetadata
            {
                Stale = false,
                ObservedAt = DateTimeOffset.UnixEpoch,
                ProjectionWatermark = "wm-1",
                ReadConsistency = ReadConsistencyClass.Eventually_consistent,
            },
        };

    private static OperationTimelineEntry VisibleEntry()
        => new()
        {
            TimelineEntryId = "entry-1",
            OperationId = "op-1",
            TaskId = "task-1",
            CorrelationId = "corr-1",
            WorkspaceReference = Workspace("workspace-1"),
            StateTransition = new DiagnosticStateTransition
            {
                FromState = LifecycleState.Preparing,
                ToState = LifecycleState.Ready,
                Disposition = OperatorDispositionLabel.Available,
            },
            SanitizedResult = CanonicalErrorCategory.Success,
            Retryable = false,
            DurationMilliseconds = 99,
            EvidenceTimestamp = DateTimeOffset.UnixEpoch,
            Freshness = new FreshnessMetadata { Stale = false, ObservedAt = DateTimeOffset.UnixEpoch, ProjectionWatermark = "wm-1", ReadConsistency = ReadConsistencyClass.Eventually_consistent },
        };

    private static RedactableDiagnosticIdentifier Workspace(string? value, RedactionMetadataVisibility visibility = RedactionMetadataVisibility.Metadata_only)
        => new()
        {
            Value = value,
            Classification = DiagnosticFieldClassification.Operator_sanitized,
            Redaction = new RedactionMetadata { Visibility = visibility, ReasonCode = "none" },
        };
}
