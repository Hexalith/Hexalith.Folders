using Bunit;

using Hexalith.Folders.UI.Components;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.9 / F-6 guardrail 1 — bUnit coverage for <see cref="DegradedModeBanner"/>. Proves the banner
/// renders its testid, the fixed DEGRADED-MODE text, the checkpoint value (and an honest "unknown" fallback
/// for an absent value), an icon, and conveys its status by text + icon (never colour alone, UX-DR14).
/// </summary>
public sealed class DegradedModeBannerTests
{
    [Fact]
    public void RendersTestid_FixedText_AndCheckpointValue()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<DegradedModeBanner> rendered =
            ctx.Render<DegradedModeBanner>(p => p.Add(c => c.LastCheckpointUtc, "2024-01-01 00:00:00Z"));

        rendered.Find("[data-testid=\"incident-degraded-mode-banner\"]").ShouldNotBeNull();
        rendered.Markup.ShouldContain("DEGRADED MODE — events shown may be incomplete or out of order.");
        rendered.Find("[data-testid=\"incident-degraded-mode-checkpoint\"]").TextContent
            .ShouldContain("2024-01-01 00:00:00Z");
    }

    [Fact]
    public void AbsentCheckpoint_RendersUnknown_NotFabricated()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<DegradedModeBanner> rendered =
            ctx.Render<DegradedModeBanner>(p => p.Add(c => c.LastCheckpointUtc, (string?)null));

        // UX-DR26 freshness honesty: a null checkpoint renders "unknown", never a fabricated 0001-01-01.
        string checkpoint = rendered.Find("[data-testid=\"incident-degraded-mode-checkpoint\"]").TextContent;
        checkpoint.ShouldContain("unknown");
        checkpoint.ShouldNotContain("0001");
    }

    [Fact]
    public void ConveysStatusByTextAndIcon_NotColourAlone()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<DegradedModeBanner> rendered =
            ctx.Render<DegradedModeBanner>(p => p.Add(c => c.LastCheckpointUtc, "2024-01-01 00:00:00Z"));

        // UX-DR14: status is conveyed by text + icon, never colour alone — both must be present.
        rendered.Find("[data-testid=\"incident-degraded-mode-text\"]").ShouldNotBeNull();
        rendered.FindAll("svg").Count.ShouldBeGreaterThanOrEqualTo(1);
        rendered.Find("[data-testid=\"incident-degraded-mode-banner\"]").GetAttribute("role").ShouldBe("status");
    }
}
