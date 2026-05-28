using System.Globalization;

using AngleSharp.Dom;

using Bunit;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components;
using Hexalith.Folders.UI.Services;
using Hexalith.FrontComposer.Contracts.Attributes;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.3 / AC #5 — bUnit coverage for <see cref="OperatorDispositionBadge"/>.
/// </summary>
public sealed class OperatorDispositionBadgeTests
{
    public static TheoryData<OperatorDispositionLabel, string, BadgeSlot> Dispositions() => new()
    {
        { OperatorDispositionLabel.Auto_recovering, "Auto-recovering", BadgeSlot.Info },
        { OperatorDispositionLabel.Available, "Available", BadgeSlot.Success },
        { OperatorDispositionLabel.Degraded_but_serving, "Degraded but serving", BadgeSlot.Warning },
        { OperatorDispositionLabel.Awaiting_human, "Awaiting human", BadgeSlot.Warning },
        { OperatorDispositionLabel.Terminal_until_intervention, "Terminal until intervention", BadgeSlot.Danger },
    };

    [Theory]
    [MemberData(nameof(Dispositions))]
    public void Renders_LabelText_ForEveryDisposition(
        OperatorDispositionLabel disposition,
        string expectedLabel,
        BadgeSlot expectedSlot)
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<OperatorDispositionBadge> rendered = ctx.Render<OperatorDispositionBadge>(
            parameters => parameters.Add(p => p.Disposition, disposition));

        rendered.Markup.ShouldContain(expectedLabel);
        IElement inner = rendered.Find("[data-fc-badge-slot]");
        inner.GetAttribute("data-fc-badge-slot").ShouldBe(expectedSlot.ToString());
    }

    [Fact]
    public void Renders_AriaLabel_WithColumnHeader_WhenProvided()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            using BunitContext ctx = BadgeRenderingFixture.Create();
            IRenderedComponent<OperatorDispositionBadge> rendered = ctx.Render<OperatorDispositionBadge>(
                parameters => parameters
                    .Add(p => p.Disposition, OperatorDispositionLabel.Auto_recovering)
                    .Add(p => p.ColumnHeader, "Status"));

            IElement badge = rendered.Find("[aria-label]");
            badge.GetAttribute("aria-label").ShouldBe("Status: Auto-recovering");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Renders_NoMutationAffordances()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<OperatorDispositionBadge> rendered = ctx.Render<OperatorDispositionBadge>(
            parameters => parameters.Add(p => p.Disposition, OperatorDispositionLabel.Awaiting_human));

        rendered.FindAll("form").ShouldBeEmpty();
        rendered.FindAll("[data-fc-command]").ShouldBeEmpty();
        rendered.FindAll("[data-fc-mutation]").ShouldBeEmpty();
        rendered.Markup.ShouldContain("Awaiting human");
    }

    [Fact]
    public void Exposes_OperatorDispositionBadge_DataTestId()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        IRenderedComponent<OperatorDispositionBadge> rendered = ctx.Render<OperatorDispositionBadge>(
            parameters => parameters.Add(p => p.Disposition, OperatorDispositionLabel.Available));

        rendered.Find("[data-testid=\"operator-disposition-badge\"]").ShouldNotBeNull();
    }
}
