using AngleSharp.Dom;

using Bunit;

using Hexalith.Folders.UI.Components;
using Hexalith.Folders.UI.Services;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.4 / AC #5 — bUnit coverage for <see cref="RedactedField"/>. Proves every disclosure state
/// renders distinctly, the redacted case shows both lock + explanatory text, and a redacted field never
/// leaks its value (the F-5 / concern #11 correctness rules).
/// </summary>
public sealed class RedactedFieldTests
{
    private const string RedactedExplanation = "Hidden by tenant policy — contact your administrator";

    [Fact]
    public void Visible_RendersValue_AndNoLockIcon()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<RedactedField> rendered = Render(ctx, FieldDisclosure.Visible, value: "acme/widgets");

        rendered.Markup.ShouldContain("acme/widgets");
        Token(rendered).ShouldBe("visible");
        rendered.FindAll("svg").ShouldBeEmpty();
    }

    [Fact]
    public void Redacted_RendersLockIconAndExplanatoryText()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<RedactedField> rendered = Render(ctx, FieldDisclosure.Redacted, value: "secret-branch");

        rendered.FindAll("svg").Count.ShouldBeGreaterThanOrEqualTo(1);
        rendered.Markup.ShouldContain(RedactedExplanation);
        Token(rendered).ShouldBe("redacted");

        // redacted-implies-no-value: the supplied value must never appear in the markup.
        rendered.Markup.ShouldNotContain("secret-branch");
    }

    [Fact]
    public void Unknown_RendersDistinctly_FromRedacted()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<RedactedField> rendered = Render(ctx, FieldDisclosure.Unknown);

        rendered.Markup.ShouldContain("Unknown");
        Token(rendered).ShouldBe("unknown");
        rendered.FindAll("svg").ShouldBeEmpty();
    }

    [Fact]
    public void Missing_RendersDistinctly_FromRedactedAndUnknown()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<RedactedField> rendered = Render(ctx, FieldDisclosure.Missing);

        rendered.Markup.ShouldContain("Not recorded");
        Token(rendered).ShouldBe("missing");
        rendered.FindAll("svg").ShouldBeEmpty();
    }

    [Fact]
    public void Redacted_Unknown_Missing_ProduceThreeDistinctTokens()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        string redacted = Token(Render(ctx, FieldDisclosure.Redacted));
        string unknown = Token(Render(ctx, FieldDisclosure.Unknown));
        string missing = Token(Render(ctx, FieldDisclosure.Missing));

        new[] { redacted, unknown, missing }.Distinct(StringComparer.Ordinal).Count().ShouldBe(3);

        // The redacted render is the only one carrying a lock icon — redacted is visibly distinct.
        Render(ctx, FieldDisclosure.Redacted).FindAll("svg").Count.ShouldBeGreaterThanOrEqualTo(1);
        Render(ctx, FieldDisclosure.Unknown).FindAll("svg").ShouldBeEmpty();
        Render(ctx, FieldDisclosure.Missing).FindAll("svg").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(FieldDisclosure.Visible, "acme/widgets")]
    [InlineData(FieldDisclosure.Redacted, RedactedExplanation)]
    [InlineData(FieldDisclosure.Unknown, "Unknown")]
    [InlineData(FieldDisclosure.Missing, "Not recorded")]
    public void Renders_AriaLabel_WithColumnHeader_ForEachState(FieldDisclosure disclosure, string announced)
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<RedactedField> rendered = ctx.Render<RedactedField>(parameters => parameters
            .Add(p => p.Disclosure, disclosure)
            .Add(p => p.Value, disclosure == FieldDisclosure.Visible ? "acme/widgets" : null)
            .Add(p => p.ColumnHeader, "Branch"));

        IElement root = rendered.Find("[data-testid=\"redacted-field\"]");
        root.GetAttribute("aria-label").ShouldBe($"Branch: {announced}");
    }

    [Fact]
    public void Visible_WithoutValue_OmitsAriaLabel()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<RedactedField> rendered = ctx.Render<RedactedField>(parameters => parameters
            .Add(p => p.Disclosure, FieldDisclosure.Visible)
            .Add(p => p.ColumnHeader, "Branch"));

        // AC #4: a Visible field with no value announces nothing — the aria-label attribute is omitted entirely.
        IElement root = rendered.Find("[data-testid=\"redacted-field\"]");
        root.GetAttribute("aria-label").ShouldBeNull();
    }

    [Fact]
    public void Exposes_RedactedField_DataTestId()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<RedactedField> rendered = Render(ctx, FieldDisclosure.Visible, value: "v");

        rendered.Find("[data-testid=\"redacted-field\"]").ShouldNotBeNull();
    }

    [Fact]
    public void Renders_NoMutationAffordances()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<RedactedField> rendered = Render(ctx, FieldDisclosure.Redacted);

        rendered.FindAll("form").ShouldBeEmpty();
        rendered.FindAll("[data-fc-command]").ShouldBeEmpty();
        rendered.FindAll("[data-fc-mutation]").ShouldBeEmpty();
        rendered.FindAll("fluentdialog").ShouldBeEmpty();
    }

    private static IRenderedComponent<RedactedField> Render(BunitContext ctx, FieldDisclosure disclosure, string? value = null)
        => ctx.Render<RedactedField>(parameters =>
        {
            parameters.Add(p => p.Disclosure, disclosure);
            if (value is not null)
            {
                parameters.Add(p => p.Value, value);
            }
        });

    private static string Token(IRenderedComponent<RedactedField> rendered)
        => rendered.Find("[data-testid=\"redacted-field\"]").GetAttribute("data-fc-disclosure") ?? string.Empty;
}
