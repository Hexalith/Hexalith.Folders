using Bunit;

using Hexalith.Folders.UI.Components;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.10 / F-7 / §3.7 / UX-DR30 / UX-DR14 — bUnit coverage for <see cref="StillLoadingCancel"/>, the
/// "still loading… [Cancel]" affordance surfaced at 2 s. Proves the Cancel control is a read-only
/// <c>&lt;button type="button"&gt;</c> with an accessible name, conveys status by text + control (never colour
/// alone), trips none of the five mutation guards, and invokes <c>OnCancel</c> on activation (the page wires
/// it to a CancellationTokenSource — a read-query cancel only).
/// </summary>
public sealed class StillLoadingCancelTests
{
    [Fact]
    public void RendersStillLoadingText_AndReadOnlyCancelButton_WithAccessibleName()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<StillLoadingCancel> rendered = ctx.Render<StillLoadingCancel>();

        // UX-DR14: status conveyed by text + control, never colour alone — the "still loading…" text is present.
        rendered.Find("[data-testid=\"console-still-loading-cancel-text\"]").TextContent.ShouldContain("still loading");

        AngleSharp.Dom.IElement button = rendered.Find("[data-testid=\"console-still-loading-cancel\"]");
        // Read-only button modelled on SafeCopyId/CorrelationCopyButton: type="button", keyboard reachable
        // (a native button is in the tab order), with an accessible name (UX-DR30).
        button.GetAttribute("type").ShouldBe("button");
        button.GetAttribute("aria-label").ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Click_InvokesOnCancel()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        bool cancelled = false;
        IRenderedComponent<StillLoadingCancel> rendered = ctx.Render<StillLoadingCancel>(p => p
            .Add(c => c.OnCancel, () => cancelled = true));

        rendered.Find("[data-testid=\"console-still-loading-cancel\"]").Click();

        cancelled.ShouldBeTrue();
    }

    [Fact]
    public void Renders_InsideAriaLivePoliteStatusRegion_ForAssistiveTechAnnouncement()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<StillLoadingCancel> rendered = ctx.Render<StillLoadingCancel>();

        // UX-DR30 / UX-DR25: the affordance is announced as a polite status (not an assertive alert) so the
        // "still loading…" update reaches assistive tech without interrupting — a status region, not an alert.
        AngleSharp.Dom.IElement region = rendered.Find("[data-testid=\"console-still-loading-cancel-region\"]");
        region.GetAttribute("role").ShouldBe("status");
        region.GetAttribute("aria-live").ShouldBe("polite");
    }

    [Fact]
    public void TripsNoMutationGuard()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<StillLoadingCancel> rendered = ctx.Render<StillLoadingCancel>();

        // F-2 / AC #4 / AC #10: the cancel affordance must NOT match any of the five command-suppression
        // selectors (no form / fluentinputform / fluentdialog / [data-fc-command] / [data-fc-mutation]).
        rendered.ShouldHaveNoMutationAffordances();
    }
}
