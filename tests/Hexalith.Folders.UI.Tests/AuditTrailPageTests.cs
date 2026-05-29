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

// xUnit1051 fires on the NSubstitute arg-matcher setups below, which configure the CancellationToken
// overload of the IClient reads (the overload the page now calls after Story 6.10). These are substitute
// configuration with an Arg.Any<CancellationToken>() matcher, not cancellable test operations, so passing
// TestContext.Current.CancellationToken would be wrong here — suppress the rule for the file.
#pragma warning disable xUnit1051

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.8 / AC #1 / AC #3 / AC #5 / AC #7 / AC #8 / AC #10 / AC #11 / AC #12 / AC #13 — the folder-scoped
/// audit-trail page renders the paginated, newest-first evidence table with the full per-record field set,
/// distinct redaction affordances (redacted ≠ unknown ≠ missing, never leaking a redacted value), honest
/// freshness, cursor pagination, safe denial, the empty/unavailable states, and no mutation affordance.
/// </summary>
public sealed class AuditTrailPageTests
{
    private const string FolderId = "folder-1";

    private static readonly IReadOnlyDictionary<string, IEnumerable<string>> EmptyHeaders =
        new Dictionary<string, IEnumerable<string>>();

    [Fact]
    public void RendersTable_WithFullFieldSet_AndScopeBannerBeforeHeading()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleRecord()));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-table\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-audit-trail-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);

        // Scope-before-evidence (UX-DR4/DR6): the tenant banner renders before the single heading.
        rendered.Find("[data-testid=\"tenant-scope-banner\"]").ShouldNotBeNull();
        int bannerIndex = rendered.Markup.IndexOf("tenant-scope-banner", StringComparison.Ordinal);
        int headingIndex = rendered.Markup.IndexOf("<h1", StringComparison.Ordinal);
        bannerIndex.ShouldBeLessThan(headingIndex);

        // Full per-record field set (AC #3).
        rendered.FindAll("[data-testid=\"console-page-audit-trail-row\"]").Count.ShouldBe(1);
        rendered.Find("[data-testid=\"console-page-audit-trail-record-id\"]").TextContent.ShouldBe("audit-1");
        rendered.Find("[data-testid=\"console-page-audit-trail-result\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"console-page-audit-trail-error-category\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"console-page-audit-trail-retryable\"]").TextContent.ShouldBe("No");
        rendered.Find("[data-testid=\"console-page-audit-trail-duration\"]").TextContent.ShouldContain("42");
        rendered.Find("[data-testid=\"console-page-audit-trail-changed-path-kind\"]").TextContent.ShouldBe("Digest");
        rendered.Markup.ShouldContain("sha256:abc");

        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void ResultAndErrorCategory_RenderCanonicalTokens()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        AuditRecord record = VisibleRecord();
        record.ResultStatus = CanonicalErrorCategory.Failed_operation;
        record.SanitizedErrorCategory = CanonicalErrorCategory.Audit_access_denied;
        StubList(client, Page(truncated: false, cursor: null, record));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-row\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-audit-trail-result\"]").TextContent.ShouldBe("failed_operation");
        rendered.Find("[data-testid=\"console-page-audit-trail-error-category\"]").TextContent.ShouldBe("audit_access_denied");
    }

    [Fact]
    public void RedactionDistinctness_RedactedUnknownMissing_AreVisiblyDistinct_AndRedactedValueNotEmitted()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        AuditRecord record = VisibleRecord();
        // Actor redacted by tenant policy (even if a value leaked onto the wire, it must never reach the DOM).
        record.ActorReference = Actor("SUPER-SECRET-ACTOR", RedactionMetadataVisibility.Redacted);
        // Evidence timestamp with no real value → honest Unknown (never a fabricated 0001-01-01).
        record.EvidenceTimestamp = Timestamp(default, RedactableAuditTimestampPrecision.Exact);
        // Task not recorded → Missing.
        record.TaskId = null;
        StubList(client, Page(truncated: false, cursor: null, record));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-row\"]").ShouldNotBeNull());

        rendered.FindAll("[data-fc-disclosure=\"redacted\"]").Count.ShouldBeGreaterThanOrEqualTo(1);
        rendered.FindAll("[data-fc-disclosure=\"unknown\"]").Count.ShouldBeGreaterThanOrEqualTo(1);
        rendered.FindAll("[data-fc-disclosure=\"missing\"]").Count.ShouldBeGreaterThanOrEqualTo(1);

        // F-5 / AC #8: a redacted value is never emitted to the DOM, and no fabricated 0001 timestamp appears.
        rendered.Markup.ShouldNotContain("SUPER-SECRET-ACTOR");
        rendered.Markup.ShouldNotContain("0001-01-01");
    }

    [Fact]
    public void ForbiddenChangedPath_RendersRedacted_AndNullChangedPath_RendersMissing()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        AuditRecord forbidden = VisibleRecord();
        forbidden.AuditRecordId = "audit-forbidden";
        forbidden.ChangedPathEvidence = ChangedPath(ChangedPathEvidenceEvidenceKind.Redacted, digest: null, DiagnosticFieldClassification.Forbidden);

        AuditRecord none = VisibleRecord();
        none.AuditRecordId = "audit-none";
        none.ChangedPathEvidence = null;

        StubList(client, Page(truncated: false, cursor: null, forbidden, none));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.FindAll("[data-testid=\"console-page-audit-trail-row\"]").Count.ShouldBe(2));

        // Forbidden classification → Redacted (lock), not Missing (AC #8).
        rendered.FindAll("[data-testid=\"console-page-audit-trail-changed-path\"] [data-fc-disclosure=\"redacted\"]")
            .Count.ShouldBe(1);
        // Null changed-path evidence → honest Missing affordance (AC #3).
        rendered.FindAll("[data-testid=\"console-page-audit-trail-changed-path\"] [data-fc-disclosure=\"missing\"]")
            .Count.ShouldBe(1);
    }

    [Fact]
    public void TruncatedPage_RendersNextCursorLink()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: true, cursor: "next-cursor", VisibleRecord()));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-next\"]").ShouldNotBeNull());

        string? href = rendered.Find("[data-testid=\"console-page-audit-trail-next\"]").GetAttribute("href");
        href.ShouldNotBeNull();
        href!.ShouldContain("cursor=next-cursor");
        href.ShouldContain("/folders/folder-1/audit-trail");
    }

    [Fact]
    public void NonTruncatedPage_RendersEndOfResults()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleRecord()));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-end\"]").ShouldNotBeNull());

        rendered.FindAll("[data-testid=\"console-page-audit-trail-next\"]").ShouldBeEmpty();
    }

    [Fact]
    public void EmptyEntries_RendersNoMatches()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-fc-empty-reason=\"no_matches\"]").ShouldNotBeNull());

        rendered.FindAll("[data-testid=\"console-page-audit-trail-table\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"console-page-audit-trail-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
    }

    [Fact]
    public void DeniedRead_RendersSafeDenial_WithoutTable()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        const string body = """{"category":"audit_access_denied","correlationId":"corr-y","retryable":false}""";
        client.ListAuditTrailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HexalithFoldersApiException("denied", 403, body, EmptyHeaders, innerException: null));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-error-panel\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-error-category\"]").TextContent.ShouldBe("audit_access_denied");
        rendered.FindAll("[data-testid=\"console-page-audit-trail-table\"]").ShouldBeEmpty();
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void TransportFailure_RendersReadModelUnavailable()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.ListAuditTrailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("connection refused"));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-fc-empty-reason=\"read_model_unavailable\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-audit-trail-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
    }

    [Fact]
    public void StaleEvidence_RendersStaleFreshness_NotCurrent()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        AuditTrailPage page = Page(truncated: false, cursor: null, VisibleRecord());
        page.Freshness = new FreshnessMetadata
        {
            Stale = true,
            ObservedAt = DateTimeOffset.UnixEpoch,
            ProjectionWatermark = "wm-1",
            ReadConsistency = ReadConsistencyClass.Eventually_consistent,
        };
        StubList(client, page);

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-freshness\"]").ShouldNotBeNull());

        string freshness = rendered.Find("[data-testid=\"console-page-audit-trail-freshness\"]").TextContent;
        freshness.ShouldContain("Stale");
        freshness.ShouldNotContain("Current");
    }

    [Fact]
    public void MissingFreshness_RendersUnknown_NotCurrentNorFabricatedTimestamp()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        AuditTrailPage page = Page(truncated: false, cursor: null, VisibleRecord());
        page.Freshness = null!;
        StubList(client, page);

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-freshness\"]").ShouldNotBeNull());

        string freshness = rendered.Find("[data-testid=\"console-page-audit-trail-freshness\"]").TextContent;
        freshness.ShouldContain("Unknown");
        freshness.ShouldNotContain("Current");
        freshness.ShouldNotContain("0001");
    }

    [Fact]
    public void IncomingCursorQueryParam_ReachesAuditTrailRead()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleRecord()));

        // AC #5: the [SupplyParameterFromQuery] cursor arrives via the URL, not the parameter builder.
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/folders/folder-1/audit-trail?cursor=cur-1");

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-table\"]").ShouldNotBeNull());

        // The incoming cursor must drive the projection read — a dropped cursor would silently break paging.
        client.Received(1).ListAuditTrailAsync(
            "folder-1", Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
            Arg.Is<string>(c => c == "cur-1"), Arg.Any<int?>(), Arg.Is<string>(f => f == null), Arg.Any<CancellationToken>());

        // No client-side hiding of returned rows (AC #5).
        rendered.FindAll("[data-testid=\"console-page-audit-trail-row\"]").Count.ShouldBe(1);
    }

    [Fact]
    public void AuditTrailRead_AlwaysPassesNullFilter()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleRecord()));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-table\"]").ShouldNotBeNull());

        // AC #6 / C4: the filter vocabulary is rejection-only — the page must pass filter:null on every call
        // (a populated filter returns validation_error). The literal null pins the rejection-only contract.
        client.Received(1).ListAuditTrailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
            Arg.Any<string>(), Arg.Any<int?>(), Arg.Is<string>(f => f == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AuditTrailPage_RendersNoFilterControl()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleRecord()));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-table\"]").ShouldNotBeNull());

        // AC #6: "filtered" is a server capability only — the page MUST NOT render any filter control that
        // implies unsupported client filtering works.
        rendered.FindAll("input").ShouldBeEmpty();
        rendered.FindAll("select").ShouldBeEmpty();
        rendered.FindAll("textarea").ShouldBeEmpty();
        rendered.FindAll("[type=\"search\"]").ShouldBeEmpty();
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void ReadCancellation_RendersReadModelUnavailable()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // AC #12: a cancelled/timed-out primary read is a transport failure, not a canonical denial — it must
        // degrade to read-model-unavailable (the same branch the backend-less E2E smoke host relies on).
        client.ListAuditTrailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-fc-empty-reason=\"read_model_unavailable\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-audit-trail-root\"]").ShouldNotBeNull();
        rendered.FindAll("[data-testid=\"console-page-audit-trail-table\"]").ShouldBeEmpty();
        rendered.FindAll("h1").Count.ShouldBe(1);
    }

    [Fact]
    public void RendersCrossLink_ToOperationTimeline_WithFolderScopedHref()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleRecord()));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        // AC #16 / UX-DR19: connected evidence — the audit page cross-links to the operation-timeline page.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-timeline-link\"]").ShouldNotBeNull());
        rendered.Find("[data-testid=\"console-page-audit-trail-timeline-link\"]")
            .GetAttribute("href").ShouldBe("/folders/folder-1/operation-timeline");
    }

    [Fact]
    public void TenantScope_RendersAccessorTenant_NotFolderIdRoute()
    {
        // Accessor tenant deliberately differs from the {FolderId} route value.
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create(tenantId: "tenant-zeta");
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleRecord()));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

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

        StubList(client, Page(truncated: false, cursor: null, VisibleRecord()));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-table\"]").ShouldNotBeNull());

        // AC #14: the audit DTO carries no distinct provider field — folder context is the route. Do not
        // fabricate a provider column.
        List<string> headers = rendered
            .FindAll("[data-testid=\"console-page-audit-trail-table\"] th")
            .Select(h => h.TextContent.Trim())
            .ToList();
        headers.ShouldNotContain(h => h.Contains("Provider", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WhilePrimaryReadPending_RendersLoadingBusyIndicator()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        TaskCompletionSource<AuditTrailPage> pending = new();
        client.ListAuditTrailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(pending.Task);

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        // AC #12: while the primary read is pending, the page shows an aria-busy loading indicator, no table,
        // and still renders its root + single <h1>.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-loading\"]").ShouldNotBeNull());
        rendered.Find("[data-testid=\"console-page-audit-trail-loading\"]").GetAttribute("aria-busy").ShouldBe("true");
        rendered.FindAll("[data-testid=\"console-page-audit-trail-table\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"console-page-audit-trail-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);

        // Resolving the read transitions off the loading branch.
        pending.SetResult(Page(truncated: false, cursor: null, VisibleRecord()));
        rendered.WaitForAssertion(() =>
        {
            rendered.Find("[data-testid=\"console-page-audit-trail-table\"]").ShouldNotBeNull();
            rendered.FindAll("[data-testid=\"console-page-audit-trail-loading\"]").ShouldBeEmpty();
        });
    }

    [Fact]
    public void VisibleTimestamp_RendersFormattedUtcValue()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleRecord()));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-table\"]").ShouldNotBeNull());

        // AC #3: a Visible exact evidence timestamp renders its invariant "u"-format value (the positive
        // happy-path — the redacted/unknown branches are covered by RedactionDistinctness_... above).
        rendered.Find("[data-testid=\"console-page-audit-trail-timestamp\"]").TextContent
            .ShouldContain("1970-01-01 00:00:00Z");
    }

    [Fact]
    public void RedactedOrUnavailableChangedPathKind_WithNonForbiddenClassification_RendersConsistentAffordance()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // AC #8: the SDK guarantees digest/reference are absent for the redacted/unavailable kinds, so the
        // disclosure must follow the EvidenceKind — not degrade to Missing on a non-forbidden classification,
        // which would make the "Redacted"/"Unavailable" kind label contradict a "Not recorded" affordance.
        AuditRecord redacted = VisibleRecord();
        redacted.AuditRecordId = "audit-redacted-kind";
        redacted.ChangedPathEvidence = ChangedPath(ChangedPathEvidenceEvidenceKind.Redacted, digest: null, DiagnosticFieldClassification.Operator_sanitized);

        AuditRecord unavailable = VisibleRecord();
        unavailable.AuditRecordId = "audit-unavailable-kind";
        unavailable.ChangedPathEvidence = ChangedPath(ChangedPathEvidenceEvidenceKind.Unavailable, digest: null, DiagnosticFieldClassification.Operator_sanitized);

        StubList(client, Page(truncated: false, cursor: null, redacted, unavailable));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.FindAll("[data-testid=\"console-page-audit-trail-row\"]").Count.ShouldBe(2));

        // The redacted-kind row shows the policy-hidden lock affordance (never the missing affordance).
        rendered.FindAll("[data-testid=\"console-page-audit-trail-changed-path\"] [data-fc-disclosure=\"redacted\"]")
            .Count.ShouldBe(1);
        // The unavailable-kind row shows the honest unknown affordance.
        rendered.FindAll("[data-testid=\"console-page-audit-trail-changed-path\"] [data-fc-disclosure=\"unknown\"]")
            .Count.ShouldBe(1);
        // Neither changed-path cell contradicts its kind label with a "missing"/"Not recorded" affordance.
        rendered.FindAll("[data-testid=\"console-page-audit-trail-changed-path\"] [data-fc-disclosure=\"missing\"]")
            .ShouldBeEmpty();
    }

    [Fact]
    public void PrimaryRead_ReceivesCancellationToken()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        StubList(client, Page(truncated: false, cursor: null, VisibleRecord()));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-table\"]").ShouldNotBeNull());

        // Story 6.10 AC #5/#14: the primary read is threaded the page's per-load CancellationToken so the
        // F-7 Cancel affordance can abort the in-flight request.
        client.Received(1).ListAuditTrailAsync(
            "folder-1", Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
            Arg.Any<string>(), Arg.Any<int?>(), Arg.Is<string>(f => f == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CancelDuringLoad_RendersNeutralCancelledReloadState_NotErrorNorUnavailable()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;
        ControllableTimeProvider clock = (ControllableTimeProvider)ctx.Services.GetRequiredService<TimeProvider>();

        // The primary read observes the token and only completes (by throwing) when the operator cancels —
        // exactly the in-flight read the F-7 Cancel affordance aborts.
        client.ListAuditTrailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Is<string>(f => f == null), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                CancellationToken ct = ci.Arg<CancellationToken>();
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return (AuditTrailPage)null!;
            });

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        // The loading branch renders SkeletonState with the page's preserved loading testid.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-loading\"]").ShouldNotBeNull());

        // Advance past the 2 s threshold so "still loading… [Cancel]" appears, then cancel.
        clock.Advance(TimeSpan.FromSeconds(2));
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-still-loading-cancel\"]").ShouldNotBeNull());
        rendered.Find("[data-testid=\"console-still-loading-cancel\"]").Click();

        // AC #5: Cancel resolves to the neutral cancelled state — a stable, non-error idle view with a
        // read-only reload — NOT the safe-denial panel and NOT the read-model-unavailable empty state.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-reload\"]").ShouldNotBeNull());
        rendered.FindAll("[data-testid=\"console-error-panel\"]").ShouldBeEmpty();
        rendered.FindAll("[data-fc-empty-reason=\"read_model_unavailable\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"console-page-audit-trail-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void SupplementaryReads_ReceiveCancellationToken()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // A populated primary read lets the load proceed cleanly through to a rendered table.
        StubList(client, Page(truncated: false, cursor: null, VisibleRecord()));

        IRenderedComponent<AuditTrail> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-audit-trail-table\"]").ShouldNotBeNull());

        // Story 6.10 AC #5/#14: the per-load CancellationToken is threaded not only into the primary
        // ListAuditTrailAsync read but into the supplementary TryReadAsync reads too — proven here via
        // GetEffectivePermissionsAsync, the unconditional advisory scope-banner read that runs on every load.
        client.Received(1).GetEffectivePermissionsAsync(
            "folder-1", Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>());
    }

    private static IRenderedComponent<AuditTrail> Render(BunitContext ctx)
        => ctx.Render<AuditTrail>(p => p.Add(c => c.FolderId, FolderId));

    private static void StubList(IClient client, AuditTrailPage page)
        => client.ListAuditTrailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(page);

    private static AuditTrailPage Page(bool truncated, string? cursor, params AuditRecord[] records)
        => new()
        {
            Entries = [.. records],
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

    private static AuditRecord VisibleRecord()
        => new()
        {
            AuditRecordId = "audit-1",
            ActorReference = Actor("actor-1"),
            TaskId = "task-1",
            OperationId = Operation("op-1"),
            CorrelationId = "corr-1",
            ResultStatus = CanonicalErrorCategory.Success,
            SanitizedErrorCategory = CanonicalErrorCategory.Success,
            Retryable = false,
            DurationMilliseconds = 42,
            EvidenceTimestamp = Timestamp(DateTimeOffset.UnixEpoch, RedactableAuditTimestampPrecision.Exact),
            Redaction = Marker(RedactionMetadataVisibility.Metadata_only),
            ChangedPathEvidence = ChangedPath(ChangedPathEvidenceEvidenceKind.Digest, "sha256:abc", DiagnosticFieldClassification.Operator_sanitized),
            Freshness = new FreshnessMetadata { Stale = false, ObservedAt = DateTimeOffset.UnixEpoch, ProjectionWatermark = "wm-1", ReadConsistency = ReadConsistencyClass.Eventually_consistent },
        };

    private static RedactableAuditActorReference Actor(string? value, RedactionMetadataVisibility visibility = RedactionMetadataVisibility.Metadata_only)
        => new()
        {
            Value = value,
            Classification = DiagnosticFieldClassification.Operator_sanitized,
            Redaction = Marker(visibility),
        };

    private static RedactableAuditOperationReference Operation(string? value, RedactionMetadataVisibility visibility = RedactionMetadataVisibility.Metadata_only)
        => new()
        {
            Value = value,
            Classification = DiagnosticFieldClassification.Operator_sanitized,
            Redaction = Marker(visibility),
        };

    private static RedactableAuditTimestamp Timestamp(DateTimeOffset value, RedactableAuditTimestampPrecision precision)
        => new()
        {
            Value = value,
            Precision = precision,
            Redaction = Marker(RedactionMetadataVisibility.Metadata_only),
        };

    private static ChangedPathEvidence2 ChangedPath(ChangedPathEvidenceEvidenceKind kind, string? digest, DiagnosticFieldClassification classification)
        => new()
        {
            EvidenceKind = kind,
            Digest = digest,
            Reference = null,
            Classification = classification,
        };

    private static RedactionMetadata Marker(RedactionMetadataVisibility visibility)
        => new() { Visibility = visibility, ReasonCode = "none" };
}
