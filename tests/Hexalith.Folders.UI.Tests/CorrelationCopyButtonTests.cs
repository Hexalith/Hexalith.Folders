using Bunit;

using Hexalith.Folders.UI.Components;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.9 / F-6 guardrail 3 — bUnit coverage for <see cref="CorrelationCopyButton"/>. Proves the
/// affordance is a read-only <c>&lt;button type="button"&gt;</c> with an accessible label, trips none of the
/// mutation guards, and copies ONLY the composed metadata-only payload (correlation id + UTC time window).
/// </summary>
public sealed class CorrelationCopyButtonTests
{
    private const string ClipboardFunction = "navigator.clipboard.writeText";

    [Fact]
    public void RendersReadOnlyButton_WithAccessibleLabel_NoMutationAffordance()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<CorrelationCopyButton> rendered = ctx.Render<CorrelationCopyButton>(p => p
            .Add(c => c.CorrelationId, "corr-1")
            .Add(c => c.TimeWindow, "win-1"));

        AngleSharp.Dom.IElement button = rendered.Find("[data-testid=\"incident-correlation-copy\"]");
        button.GetAttribute("type").ShouldBe("button");
        button.GetAttribute("aria-label").ShouldNotBeNullOrWhiteSpace();
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void Copies_ComposedMetadataOnlyPayload_CorrelationAndWindow()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<CorrelationCopyButton> rendered = ctx.Render<CorrelationCopyButton>(p => p
            .Add(c => c.CorrelationId, "corr-1")
            .Add(c => c.TimeWindow, "2024-01-01 00:00:00Z..2024-01-02 00:00:00Z"));

        rendered.Find("[data-testid=\"incident-correlation-copy\"]").Click();

        rendered.WaitForAssertion(() =>
        {
            JSRuntimeInvocation invocation = ctx.JSInterop.VerifyInvoke(ClipboardFunction);
            invocation.Arguments.Count.ShouldBe(1);
            invocation.Arguments[0].ShouldBe(
                "correlationId=corr-1; window=2024-01-01 00:00:00Z..2024-01-02 00:00:00Z");
        });
    }

    [Fact]
    public void RendersHumanLabel_NotTheRawPayload_InTheDom()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<CorrelationCopyButton> rendered = ctx.Render<CorrelationCopyButton>(p => p
            .Add(c => c.CorrelationId, "corr-1")
            .Add(c => c.TimeWindow, "2024-01-01 00:00:00Z..2024-01-02 00:00:00Z"));

        // AC #5 / AC #12: a short human label sits next to the button; the composed metadata-only payload is
        // copied via JS ONLY and must never be rendered into the DOM (no leak, no whole-payload display).
        rendered.Find("[data-testid=\"incident-correlation-copy-label\"]").TextContent.ShouldNotBeNullOrWhiteSpace();
        rendered.Markup.ShouldNotContain("correlationId=");
        rendered.Markup.ShouldNotContain("window=");
    }

    [Fact]
    public void AbsentWindow_CopiesUnknownWindow_NeverFabricated()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<CorrelationCopyButton> rendered = ctx.Render<CorrelationCopyButton>(p => p
            .Add(c => c.CorrelationId, "corr-1")
            .Add(c => c.TimeWindow, (string?)null));

        rendered.Find("[data-testid=\"incident-correlation-copy\"]").Click();

        rendered.WaitForAssertion(() =>
        {
            JSRuntimeInvocation invocation = ctx.JSInterop.VerifyInvoke(ClipboardFunction);
            invocation.Arguments[0].ShouldBe("correlationId=corr-1; window=unknown");
        });
    }
}
