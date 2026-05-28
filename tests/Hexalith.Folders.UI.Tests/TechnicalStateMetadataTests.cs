using AngleSharp.Dom;

using Bunit;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components;
using Hexalith.Folders.UI.Services;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.3 / AC #5 — bUnit coverage for <see cref="TechnicalStateMetadata"/>.
/// </summary>
public sealed class TechnicalStateMetadataTests
{
    public static TheoryData<LifecycleState> LifecycleStates()
    {
        TheoryData<LifecycleState> data = new();
        foreach (LifecycleState value in Enum.GetValues<LifecycleState>())
        {
            data.Add(value);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(LifecycleStates))]
    public void Renders_WireName_ForEveryLifecycleState(LifecycleState state)
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<TechnicalStateMetadata> rendered = ctx.Render<TechnicalStateMetadata>(
            parameters => parameters.Add(p => p.State, state));

        string expectedWireName = DispositionLabelMapper.ResolveTechnicalStateLabel(state);
        rendered.Markup.ShouldContain(expectedWireName);

        IElement root = rendered.Find("[data-testid=\"technical-state-metadata\"]");
        root.GetAttribute("data-fc-technical-state").ShouldBe(expectedWireName);
    }

    [Fact]
    public void IncludePrefixFalse_RendersBareWireName()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<TechnicalStateMetadata> rendered = ctx.Render<TechnicalStateMetadata>(
            parameters => parameters
                .Add(p => p.State, LifecycleState.Locked)
                .Add(p => p.IncludePrefix, false));

        IElement root = rendered.Find("[data-testid=\"technical-state-metadata\"]");
        root.TextContent.Trim().ShouldBe("locked");
    }

    [Fact]
    public void Renders_AriaLabel_WithColumnHeader_WhenProvided()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<TechnicalStateMetadata> rendered = ctx.Render<TechnicalStateMetadata>(
            parameters => parameters
                .Add(p => p.State, LifecycleState.Failed)
                .Add(p => p.ColumnHeader, "State"));

        IElement root = rendered.Find("[data-testid=\"technical-state-metadata\"]");
        root.GetAttribute("aria-label").ShouldBe("State: failed");
    }

    [Fact]
    public void Exposes_TechnicalStateMetadata_DataTestId_And_DataFcTechnicalState()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<TechnicalStateMetadata> rendered = ctx.Render<TechnicalStateMetadata>(
            parameters => parameters.Add(p => p.State, LifecycleState.Reconciliation_required));

        IElement root = rendered.Find("[data-testid=\"technical-state-metadata\"]");
        root.GetAttribute("data-fc-technical-state").ShouldBe("reconciliation_required");
    }
}
