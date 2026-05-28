using Bunit;

using Hexalith.Folders.UI.Components;
using Hexalith.Folders.UI.Components.Models;
using Hexalith.Folders.UI.Services;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.6 / AC #5 — the Trust Matrix renders the six dimensions as grouped evidence with a
/// non-color-only state (icon + badge + label) and a connected-evidence link, and stays read-only.
/// </summary>
public sealed class TrustMatrixTests
{
    public static TheoryData<TrustDimensionState> DimensionStates => [.. Enum.GetValues<TrustDimensionState>()];

    [Fact]
    public void RendersOneCellPerDimension_WithEvidenceLink()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IReadOnlyList<TrustMatrixCell> cells =
        [
            new("Tenant boundary", TrustDimensionState.Ready, "ok", DateTimeOffset.UnixEpoch, null, null),
            new("Audit traceability", TrustDimensionState.Unknown, "later", null, "/folders/f/audit-trail", "Audit trail"),
        ];

        IRenderedComponent<TrustMatrix> rendered = ctx.Render<TrustMatrix>(p => p.Add(m => m.Cells, cells));

        rendered.Find("[data-testid=\"trust-matrix\"]").ShouldNotBeNull();
        rendered.FindAll("[data-testid=\"trust-matrix-cell\"]").Count.ShouldBe(2);
        rendered.Markup.ShouldContain("Ready");
        rendered.Find("a[href=\"/folders/f/audit-trail\"]").ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(DimensionStates))]
    public void RendersIconAndBadge_ForEveryState(TrustDimensionState state)
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IReadOnlyList<TrustMatrixCell> cells =
        [
            new("Dimension", state, "reason", DateTimeOffset.UnixEpoch, null, null),
        ];

        IRenderedComponent<TrustMatrix> rendered = ctx.Render<TrustMatrix>(p => p.Add(m => m.Cells, cells));

        AngleSharp.Dom.IElement cell = rendered.Find("[data-testid=\"trust-matrix-cell\"]");
        cell.QuerySelectorAll("svg").Length.ShouldBeGreaterThan(0);
        cell.TextContent.ShouldContain(TrustDimensionStateMapper.ResolveLabel(state));
    }

    [Fact]
    public void Renders_NoMutationAffordances()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IReadOnlyList<TrustMatrixCell> cells =
        [
            new("Tenant boundary", TrustDimensionState.Ready, "ok", DateTimeOffset.UnixEpoch, null, null),
        ];

        IRenderedComponent<TrustMatrix> rendered = ctx.Render<TrustMatrix>(p => p.Add(m => m.Cells, cells));

        rendered.ShouldHaveNoMutationAffordances();
    }
}
