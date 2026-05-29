using Bunit;

using Hexalith.Folders.UI.Components;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.10 / F-7 / §3.7 / UX-DR25 / UX-DR14 — bUnit coverage for <see cref="SkeletonState"/>, the timed
/// perceived-wait component. Drives the 400 ms / 2 s thresholds deterministically through a hand-rolled
/// <see cref="ControllableTimeProvider"/> (no package added) and proves the three bands, layout stability
/// (the page's loading testid stays on a stable root region), the reachable Cancel affordance, and that the
/// component disposes its timers on teardown (no leaked circuit timers).
/// </summary>
public sealed class SkeletonStateTests
{
    private const string LoadingTestId = "console-page-workspace-loading";
    private const string Label = "workspace summary";

    private static (BunitContext Ctx, ControllableTimeProvider Clock) Create()
    {
        BunitContext ctx = BadgeRenderingFixture.Create();
        ControllableTimeProvider clock = (ControllableTimeProvider)ctx.Services.GetRequiredService<TimeProvider>();
        return (ctx, clock);
    }

    private static IRenderedComponent<SkeletonState> Render(BunitContext ctx)
        => ctx.Render<SkeletonState>(p => p
            .Add(c => c.Label, Label)
            .Add(c => c.TestId, LoadingTestId));

    [Fact]
    public void BeforeSkeletonDelay_RendersOnlyLabelledBusyRegion_NoBarsNoSpinnerNoCancel()
    {
        (BunitContext ctx, _) = Create();
        using BunitContext _ctx = ctx;

        IRenderedComponent<SkeletonState> rendered = Render(ctx);

        // Band (a) ≤ 400 ms (AC #2/#3): only the minimal labelled aria-busy region — no skeleton bars, no
        // spinner, no cancel — so assistive tech announces loading and the page's loading testid resolves.
        AngleSharp.Dom.IElement root = rendered.Find($"[data-testid=\"{LoadingTestId}\"]");
        root.GetAttribute("aria-busy").ShouldBe("true");
        rendered.Find("[data-testid=\"console-skeleton-label\"]").TextContent.ShouldContain(Label);
        rendered.FindAll("[data-testid=\"console-skeleton-bars\"]").ShouldBeEmpty();
        rendered.FindAll("[data-testid=\"console-still-loading-cancel\"]").ShouldBeEmpty();
        rendered.FindAll("svg").ShouldBeEmpty();
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void AfterSkeletonDelay_RendersLayoutStableSkeleton_WithLabel_StillNoCancel()
    {
        (BunitContext ctx, ControllableTimeProvider clock) = Create();
        using BunitContext _ctx = ctx;

        IRenderedComponent<SkeletonState> rendered = Render(ctx);

        clock.Advance(TimeSpan.FromMilliseconds(400));

        // Band (b) 400 ms – 2 s: the layout-stable skeleton appears; the cancel affordance does NOT yet.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-skeleton-bars\"]").ShouldNotBeNull());
        rendered.FindAll("[data-testid=\"console-still-loading-cancel\"]").ShouldBeEmpty();
        // The accessible loading label (UX-DR25) and the stable root testid persist across the band change.
        rendered.Find("[data-testid=\"console-skeleton-label\"]").TextContent.ShouldContain(Label);
        rendered.Find($"[data-testid=\"{LoadingTestId}\"]").GetAttribute("aria-busy").ShouldBe("true");
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void AfterCancelDelay_RendersSkeletonPlusStillLoadingCancel()
    {
        (BunitContext ctx, ControllableTimeProvider clock) = Create();
        using BunitContext _ctx = ctx;

        IRenderedComponent<SkeletonState> rendered = Render(ctx);

        clock.Advance(TimeSpan.FromMilliseconds(400));
        clock.Advance(TimeSpan.FromMilliseconds(1600));

        // Band (c) ≥ 2 s: skeleton bars PLUS the "still loading… [Cancel]" affordance (F-7).
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-still-loading-cancel\"]").ShouldNotBeNull());
        rendered.Find("[data-testid=\"console-skeleton-bars\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"console-still-loading-cancel-text\"]").TextContent.ShouldContain("still loading");
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void RootTestId_IsStableAcrossAllBands_LayoutStability()
    {
        (BunitContext ctx, ControllableTimeProvider clock) = Create();
        using BunitContext _ctx = ctx;

        IRenderedComponent<SkeletonState> rendered = Render(ctx);

        // UX-DR25 layout stability: the same labelled root region (carrying the page's loading testid) is
        // present in every band, so no content jump / focus loss occurs as the skeleton then cancel appear.
        rendered.FindAll($"[data-testid=\"{LoadingTestId}\"]").Count.ShouldBe(1);

        clock.Advance(TimeSpan.FromMilliseconds(400));
        rendered.WaitForAssertion(() =>
            rendered.FindAll($"[data-testid=\"{LoadingTestId}\"]").Count.ShouldBe(1));

        clock.Advance(TimeSpan.FromMilliseconds(1600));
        rendered.WaitForAssertion(() =>
            rendered.FindAll($"[data-testid=\"{LoadingTestId}\"]").Count.ShouldBe(1));
    }

    [Fact]
    public void CancelControl_AfterCancelDelay_InvokesOnCancel()
    {
        (BunitContext ctx, ControllableTimeProvider clock) = Create();
        using BunitContext _ctx = ctx;

        bool cancelled = false;
        IRenderedComponent<SkeletonState> rendered = ctx.Render<SkeletonState>(p => p
            .Add(c => c.Label, Label)
            .Add(c => c.TestId, LoadingTestId)
            .Add(c => c.OnCancel, () => cancelled = true));

        clock.Advance(TimeSpan.FromMilliseconds(400));
        clock.Advance(TimeSpan.FromMilliseconds(1600));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-still-loading-cancel\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-still-loading-cancel\"]").Click();

        // The page wires OnCancel to its CancellationTokenSource.Cancel(); proving the callback fires through
        // SkeletonState → StillLoadingCancel is the component-level half of that contract.
        cancelled.ShouldBeTrue();
    }

    [Fact]
    public void RootRegion_CarriesAriaLivePolite_ForAssistiveTechAnnouncement()
    {
        (BunitContext ctx, _) = Create();
        using BunitContext _ctx = ctx;

        IRenderedComponent<SkeletonState> rendered = Render(ctx);

        // UX-DR25: the labelled loading region is announced to assistive tech as a polite live region (the
        // accessible loading announcement is the label + aria-busy + aria-live, never colour alone, UX-DR14).
        rendered.Find($"[data-testid=\"{LoadingTestId}\"]").GetAttribute("aria-live").ShouldBe("polite");
    }

    [Fact]
    public void Dispose_DisposesBothTimers_NoLeakedCircuitTimers()
    {
        // The clock is held independently of the context, so disposing the context (which tears down the
        // rendered component → SkeletonState.Dispose()) still lets us observe the timer count afterwards.
        (BunitContext ctx, ControllableTimeProvider clock) = Create();

        Render(ctx);

        // OnInitialized schedules exactly the two one-shot timers (400 ms skeleton + 2 s cancel).
        clock.RegisteredTimerCount.ShouldBe(2);

        // Tearing the component down must dispose both ITimers so a teardown mid-wait never fires a callback
        // into a torn-down circuit (AC #2 disposal requirement).
        ctx.Dispose();

        clock.RegisteredTimerCount.ShouldBe(0);

        // A clock advance after disposal fires nothing (the timers are gone) — no exception, no late render.
        clock.Advance(TimeSpan.FromSeconds(5));
        clock.RegisteredTimerCount.ShouldBe(0);
    }
}
