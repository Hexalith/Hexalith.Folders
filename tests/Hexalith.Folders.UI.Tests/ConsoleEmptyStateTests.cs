using Bunit;

using Hexalith.Folders.UI.Components;
using Hexalith.Folders.UI.Services;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.6 / §3.8 — the empty state renders all four distinct, reason-labelled variants — including the
/// denied-access safe state (UX-DR21) — without confirming unauthorized resource existence.
/// </summary>
public sealed class ConsoleEmptyStateTests
{
    [Theory]
    [InlineData(EmptyStateReason.NoMatches, "no_matches")]
    [InlineData(EmptyStateReason.InsufficientFilterScope, "insufficient_filter_scope")]
    [InlineData(EmptyStateReason.ReadModelUnavailable, "read_model_unavailable")]
    [InlineData(EmptyStateReason.DeniedAccess, "denied_access")]
    public void RendersEachDistinctReason(EmptyStateReason reason, string expectedToken)
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<ConsoleEmptyState> rendered = ctx.Render<ConsoleEmptyState>(p => p
            .Add(e => e.Reason, reason));

        rendered.Find($"[data-fc-empty-reason=\"{expectedToken}\"]").ShouldNotBeNull();
    }

    [Fact]
    public void DeniedAccess_DoesNotConfirmResourceExistence()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<ConsoleEmptyState> rendered = ctx.Render<ConsoleEmptyState>(p => p
            .Add(e => e.Reason, EmptyStateReason.DeniedAccess));

        rendered.Markup.ShouldContain("does not confirm");
    }
}
