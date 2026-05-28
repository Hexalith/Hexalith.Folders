using AngleSharp.Dom;

using Bunit;

using Hexalith.Folders.UI.Components.Pages;
using Hexalith.Folders.UI.Services;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.4 / AC #5 — bUnit coverage for the development-only <see cref="RedactionGallery"/> page.
/// Proves the redacted-vs-unknown-vs-missing distinction is observable on the rendered page.
/// </summary>
public sealed class RedactionGalleryTests
{
    [Fact]
    public void Gallery_RendersOneRowPerDisclosureState()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<RedactionGallery> rendered = ctx.Render<RedactionGallery>();

        int expectedRows = Enum.GetValues<FieldDisclosure>().Length;
        rendered.FindAll("[data-testid=\"redacted-field\"]").Count.ShouldBe(expectedRows);
    }

    [Fact]
    public void Gallery_RedactedRow_IsTheOnlyRowWithALockIcon()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<RedactionGallery> rendered = ctx.Render<RedactionGallery>();

        int rowsWithLockIcon = rendered.FindAll("[data-testid=\"redacted-field\"]")
            .Count(field => field.QuerySelectorAll("svg").Length > 0);

        rowsWithLockIcon.ShouldBe(1);

        IElement redactedRow = rendered.FindAll("[data-testid=\"redacted-field\"]")
            .Single(field => field.QuerySelectorAll("svg").Length > 0);
        redactedRow.GetAttribute("data-fc-disclosure").ShouldBe("redacted");
    }

    [Fact]
    public void Gallery_RendersWithoutMutationControls()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<RedactionGallery> rendered = ctx.Render<RedactionGallery>();

        rendered.FindAll("form").ShouldBeEmpty();
        rendered.FindAll("fluentinputform").ShouldBeEmpty();
        rendered.FindAll("fluentdialog").ShouldBeEmpty();
        rendered.FindAll("[data-fc-command]").ShouldBeEmpty();
        rendered.FindAll("[data-fc-mutation]").ShouldBeEmpty();
    }

    [Fact]
    public void Gallery_ExposesPageRootDataTestId()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<RedactionGallery> rendered = ctx.Render<RedactionGallery>();

        rendered.Find("[data-testid=\"console-page-redaction-gallery-root\"]").ShouldNotBeNull();
    }
}
