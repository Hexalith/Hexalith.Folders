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

// xUnit1051 fires on the NSubstitute arg-matcher setups below, which configure the CancellationToken
// overload of the IClient reads (the overload the page now calls after Story 6.10). These are substitute
// configuration with an Arg.Any<CancellationToken>() matcher, not cancellable test operations, so passing
// TestContext.Current.CancellationToken would be wrong here — suppress the rule for the file.
#pragma warning disable xUnit1051

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.6 / AC #1 / AC #3 / AC #8 / AC #11 — the Workspace view renders the trust summary, the
/// predictable UX-DR18 sections, and the trust matrix; on a denied primary read it renders the
/// safe-denial path without sections; and it registers no mutation control.
/// </summary>
public sealed class WorkspacePageTests
{
    [Fact]
    public void RendersTrustSummary_Sections_AndTrustMatrix()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Status());

        IRenderedComponent<Workspace> rendered = ctx.Render<Workspace>(p => p
            .Add(w => w.FolderId, "folder-1")
            .Add(w => w.WorkspaceId, "workspace-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"workspace-trust-summary\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-workspace-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);

        foreach (string section in new[]
        {
            "overview", "folder-metadata", "diagnosis", "audit-trail",
            "provider-readiness", "lock-task-history", "access-evidence",
        })
        {
            rendered.Find($"[data-testid=\"console-page-workspace-section-{section}\"]").ShouldNotBeNull();
        }

        rendered.Find("[data-testid=\"trust-matrix\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"metadata-only-folder-tree\"]").ShouldNotBeNull();

        // AC #5 / UX-DR19: every one of the six trust-matrix cells carries a connected-evidence link.
        rendered.FindAll("[data-testid=\"trust-matrix-cell\"] a").Count.ShouldBe(6);
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void ServerDirtyDisposition_DrivesPrimaryDisposition_AndDiagnosisBadge()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // CurrentState = Ready would derive "Available"; the server diagnostics DTO must win (AC #7).
        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Status());
        client.GetDirtyStateDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(new DirtyStateDiagnostics
            {
                Disposition = OperatorDispositionLabel.Terminal_until_intervention,
                Status = "Uncommitted changes detected",
            });

        IRenderedComponent<Workspace> rendered = ctx.Render<Workspace>(p => p
            .Add(w => w.FolderId, "folder-1")
            .Add(w => w.WorkspaceId, "workspace-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"workspace-trust-summary\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"workspace-trust-disposition\"] [data-testid=\"operator-disposition-badge\"]")
            .TextContent.ShouldContain("Terminal until intervention");
        rendered.Find("[data-testid=\"console-page-workspace-section-diagnosis\"]")
            .TextContent.ShouldContain("Uncommitted changes detected");
    }

    [Fact]
    public void PopulatedDiagnostics_RenderCleanupCommitAndProvider_ThroughThePage()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        WorkspaceStatus status = Status();
        status.ProviderOutcome = new ProviderOutcome { State = ProviderOutcomeState.Known_success };

        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(status);
        client.GetWorkspaceCleanupStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(new WorkspaceCleanupStatus { Status = CleanupStatus.Succeeded });
        client.GetCommitEvidenceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(new CommitEvidence { CommitReferenceClassification = CommitEvidenceCommitReferenceClassification.Opaque_reference });

        IRenderedComponent<Workspace> rendered = ctx.Render<Workspace>(p => p
            .Add(w => w.FolderId, "folder-1")
            .Add(w => w.WorkspaceId, "workspace-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"workspace-trust-summary\"]").ShouldNotBeNull());

        // Cleanup badge, commit-reference disclosure, and provider-readiness cell all bind through the page.
        rendered.Find("[data-testid=\"console-page-workspace-section-lock-task-history\"]")
            .TextContent.ShouldContain("Cleanup succeeded");
        rendered.Markup.ShouldContain("Opaque reference present");
        rendered.Find("[data-testid=\"trust-matrix\"]").TextContent.ShouldContain("Ready");
    }

    [Fact]
    public void DeniedWorkspaceStatus_RendersSafeDenial_WithoutSections()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        const string body = """{"category":"tenant_access_denied","correlationId":"corr-y","retryable":false}""";
        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HexalithFoldersApiException("denied", 403, body, EmptyHeaders, innerException: null));

        IRenderedComponent<Workspace> rendered = ctx.Render<Workspace>(p => p
            .Add(w => w.FolderId, "folder-1")
            .Add(w => w.WorkspaceId, "workspace-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-error-panel\"]").ShouldNotBeNull());

        rendered.FindAll("[data-testid=\"console-page-workspace-section-overview\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"console-error-category\"]").TextContent.ShouldBe("tenant_access_denied");
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void TransportFailure_RendersReadModelUnavailable()
    {
        // Story 6.11 regression guard (surfaced by the E2E responsive smoke): when the read model / API is
        // unreachable the primary workspace-status read throws HttpRequestException — the page must degrade to
        // the §3.8 read-model-unavailable empty state (root + single <h1>, no crash, no leaked transport
        // error), consistent with the sibling diagnostic pages, NOT throw to a 500.
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("connection refused"));

        IRenderedComponent<Workspace> rendered = ctx.Render<Workspace>(p => p
            .Add(w => w.FolderId, "folder-1")
            .Add(w => w.WorkspaceId, "workspace-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-fc-empty-reason=\"read_model_unavailable\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-workspace-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
        rendered.FindAll("[data-testid=\"console-error-panel\"]").ShouldBeEmpty();
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void ProviderReadinessSection_ResolvesPlaceholder_IntoProviderLink_WithFolderScopedHref()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Status());

        IRenderedComponent<Workspace> rendered = ctx.Render<Workspace>(p => p
            .Add(w => w.FolderId, "folder-1")
            .Add(w => w.WorkspaceId, "workspace-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-workspace-section-provider-readiness\"]").ShouldNotBeNull());

        // AC #11 / UX-DR19: the 6.6 "available in Story 6.7" pending placeholder is RESOLVED into a real
        // folder-scoped link to the Story 6.7 Provider readiness page (connected evidence) — the pending
        // span is gone.
        rendered.Find("[data-testid=\"console-page-workspace-provider-link\"]")
            .GetAttribute("href").ShouldBe("/folders/folder-1/provider");
        rendered.FindAll("[data-testid=\"console-page-workspace-provider-pending\"]").ShouldBeEmpty();
    }

    [Fact]
    public void AuditTrailSection_ResolvesPlaceholder_IntoFolderScopedAuditAndTimelineLinks()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Status());

        IRenderedComponent<Workspace> rendered = ctx.Render<Workspace>(p => p
            .Add(w => w.FolderId, "folder-1")
            .Add(w => w.WorkspaceId, "workspace-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-workspace-section-audit-trail\"]").ShouldNotBeNull());

        // Story 6.8 / AC #15 / AC #16 / UX-DR19: the 6.6 "available in Story 6.8" pending span is RESOLVED
        // into real folder-scoped links to the audit-trail and operation-timeline pages (connected evidence);
        // the pending span is gone and the section data-testid stays stable.
        rendered.Find("[data-testid=\"console-page-workspace-audit-trail-link\"]")
            .GetAttribute("href").ShouldBe("/folders/folder-1/audit-trail");
        rendered.Find("[data-testid=\"console-page-workspace-operation-timeline-link\"]")
            .GetAttribute("href").ShouldBe("/folders/folder-1/operation-timeline");
        rendered.FindAll("[data-testid=\"console-page-workspace-audit-trail-pending\"]").ShouldBeEmpty();
    }

    [Fact]
    public void PrimaryRead_ReceivesCancellationToken()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Status());

        IRenderedComponent<Workspace> rendered = ctx.Render<Workspace>(p => p
            .Add(w => w.FolderId, "folder-1")
            .Add(w => w.WorkspaceId, "workspace-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"workspace-trust-summary\"]").ShouldNotBeNull());

        // Story 6.10 AC #5/#14: the primary read is threaded the page's per-load CancellationToken so the
        // F-7 Cancel affordance can abort the in-flight request.
        client.Received(1).GetWorkspaceStatusAsync(
            "folder-1", "workspace-1", Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SupplementaryReads_ReceiveCancellationToken()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // A populated primary read lets the load proceed past the primary into the supplementary reads.
        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Status());

        IRenderedComponent<Workspace> rendered = ctx.Render<Workspace>(p => p
            .Add(w => w.FolderId, "folder-1")
            .Add(w => w.WorkspaceId, "workspace-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"workspace-trust-summary\"]").ShouldNotBeNull());

        // Story 6.10 AC #5/#14: the per-load CancellationToken is threaded not only into the primary read
        // but into the supplementary TryReadAsync reads too — proven here via GetFolderLifecycleStatusAsync,
        // an unconditional supplementary read that runs on every successful load.
        client.Received(1).GetFolderLifecycleStatusAsync(
            "folder-1", Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CancelDuringLoad_RendersNeutralCancelledReloadState_NotErrorNorUnavailable()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;
        ControllableTimeProvider clock = (ControllableTimeProvider)ctx.Services.GetRequiredService<TimeProvider>();

        // The primary read observes the token and only completes (by throwing) when the operator cancels —
        // exactly the in-flight read the F-7 Cancel affordance aborts.
        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                CancellationToken ct = ci.Arg<CancellationToken>();
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return (WorkspaceStatus)null!;
            });

        IRenderedComponent<Workspace> rendered = ctx.Render<Workspace>(p => p
            .Add(w => w.FolderId, "folder-1")
            .Add(w => w.WorkspaceId, "workspace-1"));

        // The loading branch renders SkeletonState with the page's preserved loading testid.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-workspace-loading\"]").ShouldNotBeNull());

        // Advance past the 2 s threshold so "still loading… [Cancel]" appears, then cancel.
        clock.Advance(TimeSpan.FromSeconds(2));
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-still-loading-cancel\"]").ShouldNotBeNull());
        rendered.Find("[data-testid=\"console-still-loading-cancel\"]").Click();

        // AC #5: Cancel resolves to the neutral cancelled state — a stable, non-error idle view with a
        // read-only reload — NOT the safe-denial panel and NOT the read-model-unavailable empty state.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-workspace-reload\"]").ShouldNotBeNull());
        rendered.FindAll("[data-testid=\"console-error-panel\"]").ShouldBeEmpty();
        rendered.FindAll("[data-fc-empty-reason=\"read_model_unavailable\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"console-page-workspace-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
        rendered.ShouldHaveNoMutationAffordances();
    }

    private static readonly IReadOnlyDictionary<string, IEnumerable<string>> EmptyHeaders =
        new Dictionary<string, IEnumerable<string>>();

    private static WorkspaceStatus Status()
        => new()
        {
            FolderId = "folder-1",
            WorkspaceId = "workspace-1",
            CurrentState = LifecycleState.Ready,
            AcceptedCommandState = new AcceptedCommandState
            {
                TaskId = "task-1",
                OperationId = "op-1",
                AcceptedAt = DateTimeOffset.UnixEpoch,
            },
            LastFailureCategory = CanonicalErrorCategory.Success,
            Freshness = new FreshnessMetadata
            {
                Stale = false,
                ObservedAt = DateTimeOffset.UnixEpoch,
                ProjectionWatermark = "wm-1",
                ReadConsistency = ReadConsistencyClass.Eventually_consistent,
            },
        };
}
