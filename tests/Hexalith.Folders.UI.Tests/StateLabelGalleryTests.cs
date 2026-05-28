using Bunit;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Pages;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.3 / AC #5 — bUnit coverage for the development-only <see cref="StateLabelGallery"/> page.
/// </summary>
public sealed class StateLabelGalleryTests
{
    [Fact]
    public void Gallery_RendersOneRowPerLifecycleState_PlusReadyLagBranch()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<StateLabelGallery> rendered = ctx.Render<StateLabelGallery>();

        int expectedRows = Enum.GetValues<LifecycleState>().Length + 1;
        int actualRows = rendered.FindAll("[data-testid=\"technical-state-metadata\"]").Count;

        actualRows.ShouldBe(expectedRows);
    }

    [Fact]
    public void Gallery_RendersWithoutMutationControls()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<StateLabelGallery> rendered = ctx.Render<StateLabelGallery>();

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
        IRenderedComponent<StateLabelGallery> rendered = ctx.Render<StateLabelGallery>();

        rendered.Find("[data-testid=\"console-page-state-label-gallery-root\"]").ShouldNotBeNull();
    }
}
