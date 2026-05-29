using System.Collections.Generic;
using System.Net.Http;

using Bunit;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Pages;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using Xunit;

// xUnit1051 fires on the NSubstitute arg-matcher setups below, which now configure the CancellationToken
// overload of the IClient reads (the overload the page calls after Story 6.10). These are substitute
// configuration with an Arg.Any<CancellationToken>() matcher, not cancellable test operations, so passing
// TestContext.Current.CancellationToken would be wrong here — suppress the rule for the file.
#pragma warning disable xUnit1051

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.6 / AC #1 / AC #2 / AC #4 / AC #10 / AC #11 — the Folder view renders identity + the
/// no-workspace-bound tree, renders the safe-denial path without leaking existence, and stays read-only.
/// </summary>
public sealed class FolderDetailPageTests
{
    [Fact]
    public void RendersFolderIdentity_AndNoWorkspaceBoundTree()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Lifecycle());

        IRenderedComponent<FolderDetail> rendered = ctx.Render<FolderDetail>(p => p.Add(d => d.FolderId, "folder-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-folder-detail-identity\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-folder-detail-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
        rendered.Find("[data-testid=\"operator-disposition-badge\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"metadata-only-folder-tree-no-context\"]").ShouldNotBeNull();
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void DeniedLifecycle_RendersSafeDenial_WithoutIdentityLeak()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        const string body = """{"category":"folder_acl_denied","correlationId":"corr-x","retryable":false}""";
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HexalithFoldersApiException("denied", 403, body, EmptyHeaders, innerException: null));

        IRenderedComponent<FolderDetail> rendered = ctx.Render<FolderDetail>(p => p.Add(d => d.FolderId, "folder-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-error-panel\"]").ShouldNotBeNull());

        rendered.FindAll("[data-testid=\"console-page-folder-detail-identity\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"console-error-category\"]").TextContent.ShouldBe("folder_acl_denied");
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void TransportFailure_RendersReadModelUnavailable()
    {
        // Story 6.11 regression guard (surfaced by the E2E responsive smoke): when the read model / API is
        // unreachable the primary lifecycle read throws HttpRequestException — the page must degrade to the
        // §3.8 read-model-unavailable empty state (root + single <h1>, no crash, no leaked transport error),
        // consistent with the sibling diagnostic pages, NOT throw to a 500.
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("connection refused"));

        IRenderedComponent<FolderDetail> rendered = ctx.Render<FolderDetail>(p => p.Add(d => d.FolderId, "folder-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-fc-empty-reason=\"read_model_unavailable\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-folder-detail-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
        rendered.FindAll("[data-testid=\"console-error-panel\"]").ShouldBeEmpty();
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void ProviderBindingRow_RendersProviderReadinessLink_WithFolderScopedHref()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Lifecycle());

        IRenderedComponent<FolderDetail> rendered = ctx.Render<FolderDetail>(p => p.Add(d => d.FolderId, "folder-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-folder-detail-identity\"]").ShouldNotBeNull());

        // AC #11: the Folder view's provider-binding row links out to the Story 6.7 Provider readiness page.
        rendered.Find("[data-testid=\"console-page-folder-detail-provider-link\"]")
            .GetAttribute("href").ShouldBe("/folders/folder-1/provider");
    }

    [Fact]
    public void FolderDetail_RendersAuditTrailAndOperationTimelineLinks_WithFolderScopedHrefs()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Lifecycle());

        IRenderedComponent<FolderDetail> rendered = ctx.Render<FolderDetail>(p => p.Add(d => d.FolderId, "folder-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-folder-detail-identity\"]").ShouldNotBeNull());

        // Story 6.8 / AC #16 / UX-DR19: the Folder view links out to the audit-trail and operation-timeline
        // pages — audit/timeline are reachable from FolderDetail, not isolated on a disconnected page.
        rendered.Find("[data-testid=\"console-page-folder-detail-audit-trail-link\"]")
            .GetAttribute("href").ShouldBe("/folders/folder-1/audit-trail");
        rendered.Find("[data-testid=\"console-page-folder-detail-operation-timeline-link\"]")
            .GetAttribute("href").ShouldBe("/folders/folder-1/operation-timeline");
    }

    [Fact]
    public void FolderDetail_RendersIncidentStreamLink_WithFolderQuery()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Lifecycle());

        IRenderedComponent<FolderDetail> rendered = ctx.Render<FolderDetail>(p => p.Add(d => d.FolderId, "folder-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-folder-detail-identity\"]").ShouldNotBeNull());

        // Story 6.9 / AC #13 / UX-DR19: the Folder view links out to the F-6 incident-mode last-resort read
        // path, supplying the folder as the ?folder= query (the only incident-stream data source is folder-scoped).
        rendered.Find("[data-testid=\"console-page-folder-detail-incident-stream-link\"]")
            .GetAttribute("href").ShouldBe("/_admin/incident-stream?folder=folder-1");
    }

    [Fact]
    public void PrimaryRead_ReceivesCancellationToken()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Lifecycle());

        IRenderedComponent<FolderDetail> rendered = ctx.Render<FolderDetail>(p => p.Add(d => d.FolderId, "folder-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-folder-detail-identity\"]").ShouldNotBeNull());

        // Story 6.10 AC #5/#14: the primary read is threaded the page's per-load CancellationToken.
        client.Received(1).GetFolderLifecycleStatusAsync(
            "folder-1", Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SupplementaryReads_ReceiveCancellationToken()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // Populate the primary lifecycle read so the load proceeds and the page renders identity.
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Lifecycle());

        IRenderedComponent<FolderDetail> rendered = ctx.Render<FolderDetail>(p => p.Add(d => d.FolderId, "folder-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-folder-detail-identity\"]").ShouldNotBeNull());

        // Story 6.10 AC #5/#14: the supplementary effective-permissions read (run unconditionally before the
        // lifecycle read to feed the tenant-scope banner) is also threaded the page's per-load CancellationToken.
        client.Received(1).GetEffectivePermissionsAsync(
            "folder-1", Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CancelDuringLoad_RendersNeutralCancelledReloadState_NotErrorNorUnavailable()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;
        ControllableTimeProvider clock = (ControllableTimeProvider)ctx.Services.GetRequiredService<TimeProvider>();

        // The primary read observes the token and only completes (by throwing) when the operator cancels.
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                CancellationToken ct = ci.Arg<CancellationToken>();
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return (FolderLifecycleStatus)null!;
            });

        IRenderedComponent<FolderDetail> rendered = ctx.Render<FolderDetail>(p => p.Add(d => d.FolderId, "folder-1"));

        // Loading branch renders SkeletonState with the page's preserved loading testid.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-folder-detail-loading\"]").ShouldNotBeNull());

        // Advance past the 2 s threshold so "still loading… [Cancel]" appears, then cancel.
        clock.Advance(TimeSpan.FromSeconds(2));
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-still-loading-cancel\"]").ShouldNotBeNull());
        rendered.Find("[data-testid=\"console-still-loading-cancel\"]").Click();

        // AC #5: Cancel → neutral cancelled state (read-only reload), NOT safe-denial, NOT read-model-unavailable.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-folder-detail-reload\"]").ShouldNotBeNull());
        rendered.FindAll("[data-testid=\"console-error-panel\"]").ShouldBeEmpty();
        rendered.FindAll("[data-fc-empty-reason=\"read_model_unavailable\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"console-page-folder-detail-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
        rendered.ShouldHaveNoMutationAffordances();
    }

    private static readonly IReadOnlyDictionary<string, IEnumerable<string>> EmptyHeaders =
        new Dictionary<string, IEnumerable<string>>();

    private static FolderLifecycleStatus Lifecycle()
        => new()
        {
            FolderId = "folder-1",
            LifecycleState = LifecycleState.Ready,
            Archived = false,
            RepositoryBindingId = "rb-1",
            ProviderBindingRef = "pb-1",
            Freshness = new FreshnessMetadata
            {
                Stale = false,
                ObservedAt = DateTimeOffset.UnixEpoch,
                ProjectionWatermark = "wm-1",
                ReadConsistency = ReadConsistencyClass.Eventually_consistent,
            },
        };
}
