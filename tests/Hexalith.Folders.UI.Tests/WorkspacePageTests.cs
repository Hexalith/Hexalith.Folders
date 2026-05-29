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

        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>())
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
        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>())
            .Returns(Status());
        client.GetDirtyStateDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>())
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

        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>())
            .Returns(status);
        client.GetWorkspaceCleanupStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>())
            .Returns(new WorkspaceCleanupStatus { Status = CleanupStatus.Succeeded });
        client.GetCommitEvidenceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>())
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
        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>())
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
    public void ProviderReadinessSection_ResolvesPlaceholder_IntoProviderLink_WithFolderScopedHref()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>())
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
