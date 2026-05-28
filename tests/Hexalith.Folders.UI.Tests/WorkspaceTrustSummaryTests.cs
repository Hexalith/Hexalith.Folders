using Bunit;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components;
using Hexalith.Folders.UI.Components.Models;
using Hexalith.Folders.UI.Services;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.6 / AC #3 / AC #7 — the Workspace Trust Summary renders a disposition badge (primary visual)
/// for every C6 lifecycle state, derives the disposition by the canonical mapper, and never promotes a
/// bare technical-state name to the primary visual.
/// </summary>
public sealed class WorkspaceTrustSummaryTests
{
    public static TheoryData<LifecycleState> LifecycleStates => [.. Enum.GetValues<LifecycleState>()];

    [Theory]
    [MemberData(nameof(LifecycleStates))]
    public void RendersDispositionBadge_ForEveryLifecycleState(LifecycleState state)
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        OperatorDispositionLabel expected = DispositionLabelMapper.ResolveDisposition(state, hasProjectionLagEvidence: false);
        string expectedLabel = DispositionLabelMapper.ResolveLabel(expected);

        IRenderedComponent<WorkspaceTrustSummary> rendered = ctx.Render<WorkspaceTrustSummary>(p => p
            .Add(s => s.Model, Model(state, hasLag: false)));

        rendered.Find("[data-testid=\"operator-disposition-badge\"]").TextContent.ShouldContain(expectedLabel);
        rendered.Find("[data-testid=\"technical-state-metadata\"]").ShouldNotBeNull();
    }

    [Fact]
    public void Ready_WithProjectionLag_IsDegradedButServing()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<WorkspaceTrustSummary> rendered = ctx.Render<WorkspaceTrustSummary>(p => p
            .Add(s => s.Model, Model(LifecycleState.Ready, hasLag: true)));

        rendered.Find("[data-testid=\"operator-disposition-badge\"]").TextContent.ShouldContain("Degraded but serving");
    }

    [Fact]
    public void UnknownProviderOutcome_RendersAwaitingHumanBadge_NotNeutralUnknown()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<WorkspaceTrustSummary> rendered = ctx.Render<WorkspaceTrustSummary>(p => p
            .Add(s => s.Model, Model(LifecycleState.Unknown_provider_outcome, hasLag: false)));

        rendered.Find("[data-testid=\"operator-disposition-badge\"]").TextContent.ShouldContain("Awaiting human");
    }

    [Fact]
    public void ServerSuppliedDisposition_OverridesDerivedDisposition()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        // Ready normally derives "Available"; force a DIFFERENT server-computed disposition (AC #7).
        OperatorDispositionLabel derived = DispositionLabelMapper.ResolveDisposition(LifecycleState.Ready, hasProjectionLagEvidence: false);
        OperatorDispositionLabel serverValue = OperatorDispositionLabel.Awaiting_human;
        serverValue.ShouldNotBe(derived);

        WorkspaceTrustSummaryModel model = Model(LifecycleState.Ready, hasLag: false) with
        {
            ServerDisposition = serverValue,
        };

        IRenderedComponent<WorkspaceTrustSummary> rendered = ctx.Render<WorkspaceTrustSummary>(p => p
            .Add(s => s.Model, model));

        string headline = rendered.Find("[data-testid=\"workspace-trust-disposition\"] [data-testid=\"operator-disposition-badge\"]").TextContent;
        headline.ShouldContain(DispositionLabelMapper.ResolveLabel(serverValue));
        headline.ShouldNotContain(DispositionLabelMapper.ResolveLabel(derived));
    }

    [Fact]
    public void Identifiers_ExposeSafeCopyAffordance()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<WorkspaceTrustSummary> rendered = ctx.Render<WorkspaceTrustSummary>(p => p
            .Add(s => s.Model, Model(LifecycleState.Ready, hasLag: false)));

        // AC #9 / UX-DR27: tenant, folder, workspace, task, and correlation each expose a read-only copy button.
        rendered.FindAll("[data-testid=\"safe-copy-button\"]").Count.ShouldBe(5);
        rendered.Find("[data-testid=\"safe-copy-button\"]").GetAttribute("aria-label").ShouldStartWith("Copy");
    }

    [Fact]
    public void UnknownLockState_RendersNeutralBadge_NotBareText()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        WorkspaceTrustSummaryModel model = Model(LifecycleState.Ready, hasLag: false) with { LockState = null };

        IRenderedComponent<WorkspaceTrustSummary> rendered = ctx.Render<WorkspaceTrustSummary>(p => p
            .Add(s => s.Model, model));

        // UX-DR14: "unknown" is still a status indicator (badge with slot + label), not a bare span.
        rendered.Find("[data-testid=\"workspace-trust-lock-unknown\"] [data-fc-badge-slot]").ShouldNotBeNull();
    }

    [Fact]
    public void DirtyState_WithServerDisposition_RendersDispositionBadge()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        WorkspaceTrustSummaryModel model = Model(LifecycleState.Dirty, hasLag: false) with
        {
            DirtyDisposition = OperatorDispositionLabel.Awaiting_human,
            DirtyState = "Uncommitted changes detected",
        };

        IRenderedComponent<WorkspaceTrustSummary> rendered = ctx.Render<WorkspaceTrustSummary>(p => p
            .Add(s => s.Model, model));

        // Dirty renders a second disposition badge (headline + dirty) plus the supplementary status text.
        rendered.FindAll("[data-testid=\"operator-disposition-badge\"]").Count.ShouldBe(2);
        rendered.Find("[data-testid=\"workspace-trust-dirty\"]").TextContent.ShouldContain("Uncommitted changes detected");
    }

    [Fact]
    public void DirtyState_WithoutServerDisposition_RendersNeutralBadge_NotBareText()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        // Model() leaves DirtyDisposition null → the dirty field falls back to a neutral badge.
        IRenderedComponent<WorkspaceTrustSummary> rendered = ctx.Render<WorkspaceTrustSummary>(p => p
            .Add(s => s.Model, Model(LifecycleState.Ready, hasLag: false)));

        rendered.Find("[data-testid=\"workspace-trust-dirty\"] [data-fc-badge-slot]").ShouldNotBeNull();
    }

    [Fact]
    public void RedactedCommitReference_RendersThroughRedactedField_NeverTheValue()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        WorkspaceTrustSummaryModel model = Model(LifecycleState.Committed, hasLag: false) with
        {
            CommitReferenceDisclosure = FieldDisclosure.Redacted,
            CommitReferenceText = "must-not-render",
        };

        IRenderedComponent<WorkspaceTrustSummary> rendered = ctx.Render<WorkspaceTrustSummary>(p => p
            .Add(s => s.Model, model));

        rendered.Markup.ShouldNotContain("must-not-render");
        rendered.FindAll("[data-fc-disclosure=\"redacted\"]").ShouldNotBeEmpty();
    }

    [Fact]
    public void Renders_NoMutationAffordances()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<WorkspaceTrustSummary> rendered = ctx.Render<WorkspaceTrustSummary>(p => p
            .Add(s => s.Model, Model(LifecycleState.Ready, hasLag: false)));

        rendered.ShouldHaveNoMutationAffordances();
    }

    private static WorkspaceTrustSummaryModel Model(LifecycleState state, bool hasLag)
        => new()
        {
            Tenant = "tenant-a",
            Folder = "folder-1",
            WorkspaceId = "workspace-1",
            CurrentState = state,
            HasProjectionLagEvidence = hasLag,
            AuthorizationPosture = TenantAccessState.Allowed,
            LockState = Hexalith.Folders.Client.Generated.LockState.Unlocked,
            DirtyState = "clean",
            TaskId = "task-1",
            CorrelationId = "corr-1",
            LatestReasonCategory = CanonicalErrorCategory.Success,
            FreshnessObservedAt = DateTimeOffset.UnixEpoch,
            ProjectionWatermark = "wm-1",
            FreshnessStale = hasLag,
        };
}
