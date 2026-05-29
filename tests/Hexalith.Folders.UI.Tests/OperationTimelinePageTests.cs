using System;
using System.Collections.Generic;
using System.Net.Http;

using Bunit;

using Hexalith.Folders.Client.Generated;
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
/// Story 6.8 / AC #2 / AC #4 / AC #5 / AC #8 / AC #9 / AC #10 / AC #11 / AC #12 — the folder-scoped
/// operation-timeline page (Diagnostic Timeline, UX-DR8) renders the paginated, newest-first timeline with
/// the operator disposition as the PRIMARY visual over the technical from→to transition, the redactable
/// workspace reference, honest freshness, cursor pagination, safe denial, the empty/unavailable states, and
/// no mutation affordance.
/// </summary>
public sealed class OperationTimelinePageTests
{
    private const string FolderId = "folder-1";

    private static readonly IReadOnlyDictionary<string, IEnumerable<string>> EmptyHeaders =
        new Dictionary<string, IEnumerable<string>>();

    [Fact]
    public void RendersTimeline_WithFullFieldSet_AndScopeBannerBeforeHeading()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-table\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-operation-timeline-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);

        rendered.Find("[data-testid=\"tenant-scope-banner\"]").ShouldNotBeNull();
        int bannerIndex = rendered.Markup.IndexOf("tenant-scope-banner", StringComparison.Ordinal);
        int headingIndex = rendered.Markup.IndexOf("<h1", StringComparison.Ordinal);
        bannerIndex.ShouldBeLessThan(headingIndex);

        rendered.FindAll("[data-testid=\"console-page-operation-timeline-row\"]").Count.ShouldBe(1);
        rendered.Find("[data-testid=\"console-page-operation-timeline-entry-id\"]").TextContent.ShouldBe("entry-1");
        rendered.Find("[data-testid=\"console-page-operation-timeline-result\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"console-page-operation-timeline-retryable\"]").TextContent.ShouldBe("No");
        rendered.Find("[data-testid=\"console-page-operation-timeline-duration\"]").TextContent.ShouldContain("99");

        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void Disposition_IsPrimaryVisual_AndUsesServerValue_NotDerivedFromToState()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // ToState = Ready would DERIVE "Available"; the server-computed disposition must win (AC #9 / F-4).
        OperationTimelineEntry entry = VisibleEntry();
        entry.StateTransition = new DiagnosticStateTransition
        {
            FromState = LifecycleState.Preparing,
            ToState = LifecycleState.Ready,
            Disposition = OperatorDispositionLabel.Awaiting_human,
        };
        StubList(client, Page(truncated: false, cursor: null, entry));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-transition\"]").ShouldNotBeNull());

        // Disposition badge is present and carries the server value (not the ToState-derived "Available").
        rendered.Find("[data-testid=\"console-page-operation-timeline-transition\"] [data-testid=\"operator-disposition-badge\"]")
            .TextContent.ShouldContain("Awaiting human");
        // Technical from→to lifecycle is the muted secondary metadata.
        rendered.FindAll("[data-testid=\"console-page-operation-timeline-transition\"] [data-fc-technical-state]")
            .Count.ShouldBe(2);
    }

    [Fact]
    public void RedactedWorkspaceReference_RendersRedacted_AndValueNotEmitted()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        OperationTimelineEntry entry = VisibleEntry();
        entry.WorkspaceReference = Workspace("SECRET-WORKSPACE", RedactionMetadataVisibility.Redacted);
        StubList(client, Page(truncated: false, cursor: null, entry));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-row\"]").ShouldNotBeNull());

        rendered.FindAll("[data-fc-disclosure=\"redacted\"]").Count.ShouldBeGreaterThanOrEqualTo(1);
        rendered.Markup.ShouldNotContain("SECRET-WORKSPACE");
    }

    [Fact]
    public void DefaultTimestamp_RendersUnknown_NotFabricated()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        OperationTimelineEntry entry = VisibleEntry();
        entry.EvidenceTimestamp = default;
        StubList(client, Page(truncated: false, cursor: null, entry));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-timestamp\"]").ShouldNotBeNull());

        string timestamp = rendered.Find("[data-testid=\"console-page-operation-timeline-timestamp\"]").TextContent;
        timestamp.ShouldContain("unknown");
        timestamp.ShouldNotContain("0001");
    }

    [Fact]
    public void TruncatedPage_RendersNextCursorLink()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: true, cursor: "next-cursor", VisibleEntry()));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-next\"]").ShouldNotBeNull());

        string? href = rendered.Find("[data-testid=\"console-page-operation-timeline-next\"]").GetAttribute("href");
        href.ShouldNotBeNull();
        href!.ShouldContain("cursor=next-cursor");
        href.ShouldContain("/folders/folder-1/operation-timeline");
    }

    [Fact]
    public void NonTruncatedPage_RendersEndOfResults()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-end\"]").ShouldNotBeNull());

        rendered.FindAll("[data-testid=\"console-page-operation-timeline-next\"]").ShouldBeEmpty();
    }

    [Fact]
    public void EmptyEntries_RendersNoMatches()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-fc-empty-reason=\"no_matches\"]").ShouldNotBeNull());

        rendered.FindAll("[data-testid=\"console-page-operation-timeline-table\"]").ShouldBeEmpty();
        rendered.FindAll("h1").Count.ShouldBe(1);
    }

    [Fact]
    public void DeniedRead_RendersSafeDenial_WithoutTable()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        const string body = """{"category":"audit_access_denied","correlationId":"corr-y","retryable":false}""";
        client.ListOperationTimelineAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new HexalithFoldersApiException("denied", 403, body, EmptyHeaders, innerException: null));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-error-panel\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-error-category\"]").TextContent.ShouldBe("audit_access_denied");
        rendered.FindAll("[data-testid=\"console-page-operation-timeline-table\"]").ShouldBeEmpty();
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void TransportFailure_RendersReadModelUnavailable()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.ListOperationTimelineAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new HttpRequestException("connection refused"));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-fc-empty-reason=\"read_model_unavailable\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-operation-timeline-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
    }

    [Fact]
    public void StaleEvidence_RendersStaleFreshness_NotCurrent()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        OperationTimelinePage page = Page(truncated: false, cursor: null, VisibleEntry());
        page.Freshness = new FreshnessMetadata
        {
            Stale = true,
            ObservedAt = DateTimeOffset.UnixEpoch,
            ProjectionWatermark = "wm-1",
            ReadConsistency = ReadConsistencyClass.Eventually_consistent,
        };
        StubList(client, page);

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-freshness\"]").ShouldNotBeNull());

        string freshness = rendered.Find("[data-testid=\"console-page-operation-timeline-freshness\"]").TextContent;
        freshness.ShouldContain("Stale");
        freshness.ShouldNotContain("Current");
    }

    [Fact]
    public void IncomingCursorQueryParam_ReachesOperationTimelineRead()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        // AC #5: the [SupplyParameterFromQuery] cursor arrives via the URL, not the parameter builder.
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/folders/folder-1/operation-timeline?cursor=cur-1");

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-table\"]").ShouldNotBeNull());

        // The incoming cursor must drive the projection read — a dropped cursor would silently break paging.
        client.Received(1).ListOperationTimelineAsync(
            "folder-1", Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
            Arg.Is<string>(c => c == "cur-1"), Arg.Any<int?>(), Arg.Is<string>(f => f == null));

        // No client-side hiding of returned rows (AC #5).
        rendered.FindAll("[data-testid=\"console-page-operation-timeline-row\"]").Count.ShouldBe(1);
    }

    [Fact]
    public void OperationTimelineRead_AlwaysPassesNullFilter()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-table\"]").ShouldNotBeNull());

        // AC #6 / C4: the filter vocabulary is rejection-only — the page must pass filter:null on every call.
        client.Received(1).ListOperationTimelineAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
            Arg.Any<string>(), Arg.Any<int?>(), Arg.Is<string>(f => f == null));
    }

    [Fact]
    public void OperationTimelinePage_RendersNoFilterControl()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-table\"]").ShouldNotBeNull());

        // AC #6: "filtered" is a server capability only — the page MUST NOT render any filter control.
        rendered.FindAll("input").ShouldBeEmpty();
        rendered.FindAll("select").ShouldBeEmpty();
        rendered.FindAll("textarea").ShouldBeEmpty();
        rendered.FindAll("[type=\"search\"]").ShouldBeEmpty();
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void MissingIdentifiers_NullTimelineEntryOperationTaskCorrelation_RenderMissingAffordances()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        OperationTimelineEntry entry = VisibleEntry();
        entry.TimelineEntryId = null;
        entry.OperationId = null;
        entry.TaskId = null;
        entry.CorrelationId = null;
        StubList(client, Page(truncated: false, cursor: null, entry));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-row\"]").ShouldNotBeNull());

        // AC #4 / AC #8: each absent identifier renders an honest Missing affordance, never blank. The
        // workspace reference stays Visible in VisibleEntry, so exactly the four absent ids report Missing.
        rendered.FindAll("[data-testid=\"console-page-operation-timeline-row\"] [data-fc-disclosure=\"missing\"]")
            .Count.ShouldBe(4);
        // The safe-copy <code> must not render for a null id.
        rendered.FindAll("[data-testid=\"console-page-operation-timeline-entry-id\"]").ShouldBeEmpty();
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void WorkspaceReference_BlankValueMetadataOnly_RendersMissing_DistinctFromRedacted()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        OperationTimelineEntry entry = VisibleEntry();
        // Blank value with metadata_only visibility resolves to Missing — never Redacted (AC #8 distinctness).
        entry.WorkspaceReference = Workspace(null);
        StubList(client, Page(truncated: false, cursor: null, entry));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-row\"]").ShouldNotBeNull());

        rendered.FindAll("[data-fc-disclosure=\"missing\"]").Count.ShouldBeGreaterThanOrEqualTo(1);
        rendered.FindAll("[data-fc-disclosure=\"redacted\"]").ShouldBeEmpty();
        rendered.Markup.ShouldContain("Not recorded");
    }

    [Fact]
    public void MissingFreshness_RendersUnknown_NotCurrentNorFabricatedTimestamp()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        OperationTimelinePage page = Page(truncated: false, cursor: null, VisibleEntry());
        page.Freshness = null!;
        StubList(client, page);

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-freshness\"]").ShouldNotBeNull());

        // AC #10: absent freshness renders Unknown — never "Current", never a fabricated 0001-01-01.
        string freshness = rendered.Find("[data-testid=\"console-page-operation-timeline-freshness\"]").TextContent;
        freshness.ShouldContain("Unknown");
        freshness.ShouldNotContain("Current");
        freshness.ShouldNotContain("0001");
    }

    [Fact]
    public void ReadCancellation_RendersReadModelUnavailable()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // AC #12: a cancelled/timed-out primary read is a transport failure, not a canonical denial.
        client.ListOperationTimelineAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new TaskCanceledException());

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-fc-empty-reason=\"read_model_unavailable\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-operation-timeline-root\"]").ShouldNotBeNull();
        rendered.FindAll("[data-testid=\"console-page-operation-timeline-table\"]").ShouldBeEmpty();
        rendered.FindAll("h1").Count.ShouldBe(1);
    }

    [Fact]
    public void MultipleEntries_RenderOneRowEach_NoHiding()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        OperationTimelineEntry e1 = VisibleEntry();
        e1.TimelineEntryId = "entry-1";
        OperationTimelineEntry e2 = VisibleEntry();
        e2.TimelineEntryId = "entry-2";
        OperationTimelineEntry e3 = VisibleEntry();
        e3.TimelineEntryId = "entry-3";
        StubList(client, Page(truncated: false, cursor: null, e1, e2, e3));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        // AC #5 / AC #12: every returned entry renders as its own row (no client-side hiding).
        rendered.WaitForAssertion(() =>
            rendered.FindAll("[data-testid=\"console-page-operation-timeline-row\"]").Count.ShouldBe(3));

        List<string> ids = rendered
            .FindAll("[data-testid=\"console-page-operation-timeline-entry-id\"]")
            .Select(e => e.TextContent)
            .ToList();
        ids.Count.ShouldBe(3);
        ids[0].ShouldContain("entry-1");
        ids[1].ShouldContain("entry-2");
        ids[2].ShouldContain("entry-3");
    }

    [Fact]
    public void WhilePrimaryReadPending_RendersLoadingBusyIndicator()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        TaskCompletionSource<OperationTimelinePage> pending = new();
        client.ListOperationTimelineAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .Returns(pending.Task);

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        // AC #12: while the primary read is pending, the page shows an aria-busy loading indicator, no table,
        // and still renders its root + single <h1>.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-loading\"]").ShouldNotBeNull());
        rendered.Find("[data-testid=\"console-page-operation-timeline-loading\"]").GetAttribute("aria-busy").ShouldBe("true");
        rendered.FindAll("[data-testid=\"console-page-operation-timeline-table\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"console-page-operation-timeline-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);

        // Resolving the read transitions off the loading branch.
        pending.SetResult(Page(truncated: false, cursor: null, VisibleEntry()));
        rendered.WaitForAssertion(() =>
        {
            rendered.Find("[data-testid=\"console-page-operation-timeline-table\"]").ShouldNotBeNull();
            rendered.FindAll("[data-testid=\"console-page-operation-timeline-loading\"]").ShouldBeEmpty();
        });
    }

    [Fact]
    public void RendersCrossLink_ToAuditTrail_WithFolderScopedHref()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        // AC #16 / UX-DR19: connected evidence — the timeline page cross-links back to the audit-trail page.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-audit-link\"]").ShouldNotBeNull());
        rendered.Find("[data-testid=\"console-page-operation-timeline-audit-link\"]")
            .GetAttribute("href").ShouldBe("/folders/folder-1/audit-trail");
    }

    [Fact]
    public void TenantScope_RendersAccessorTenant_NotFolderIdRoute()
    {
        // Accessor tenant deliberately differs from the {FolderId} route value.
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create(tenantId: "tenant-zeta");
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        // AC #7: tenant scope is sourced from IUserContextAccessor.TenantId, never the {FolderId} route.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"tenant-scope-tenant-id\"]").ShouldNotBeNull());
        string tenant = rendered.Find("[data-testid=\"tenant-scope-tenant-id\"]").TextContent;
        tenant.ShouldBe("tenant-zeta");
        tenant.ShouldNotBe("folder-1");
    }

    [Fact]
    public void Table_RendersNoFabricatedProviderColumn()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleEntry()));

        IRenderedComponent<OperationTimeline> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-operation-timeline-table\"]").ShouldNotBeNull());

        // AC #14: surface folder/workspace identity only — do not fabricate a provider column.
        List<string> headers = rendered
            .FindAll("[data-testid=\"console-page-operation-timeline-table\"] th")
            .Select(h => h.TextContent.Trim())
            .ToList();
        headers.ShouldNotContain(h => h.Contains("Provider", StringComparison.OrdinalIgnoreCase));
    }

    private static IRenderedComponent<OperationTimeline> Render(BunitContext ctx)
        => ctx.Render<OperationTimeline>(p => p.Add(c => c.FolderId, FolderId));

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
