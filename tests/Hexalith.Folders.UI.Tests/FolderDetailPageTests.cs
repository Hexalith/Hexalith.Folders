using System.Collections.Generic;

using Bunit;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Pages;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using Xunit;

// xUnit1051 fires on NSubstitute arg-matcher setups for IClient methods that have a CancellationToken
// overload; these are substitute configuration (matching the no-token overload the pages call), not
// cancellable operations, so the rule does not apply here.
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

        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>())
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
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>())
            .ThrowsAsync(new HexalithFoldersApiException("denied", 403, body, EmptyHeaders, innerException: null));

        IRenderedComponent<FolderDetail> rendered = ctx.Render<FolderDetail>(p => p.Add(d => d.FolderId, "folder-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-error-panel\"]").ShouldNotBeNull());

        rendered.FindAll("[data-testid=\"console-page-folder-detail-identity\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"console-error-category\"]").TextContent.ShouldBe("folder_acl_denied");
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void ProviderBindingRow_RendersProviderReadinessLink_WithFolderScopedHref()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>())
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

        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>())
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

        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>())
            .Returns(Lifecycle());

        IRenderedComponent<FolderDetail> rendered = ctx.Render<FolderDetail>(p => p.Add(d => d.FolderId, "folder-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-folder-detail-identity\"]").ShouldNotBeNull());

        // Story 6.9 / AC #13 / UX-DR19: the Folder view links out to the F-6 incident-mode last-resort read
        // path, supplying the folder as the ?folder= query (the only incident-stream data source is folder-scoped).
        rendered.Find("[data-testid=\"console-page-folder-detail-incident-stream-link\"]")
            .GetAttribute("href").ShouldBe("/_admin/incident-stream?folder=folder-1");
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
