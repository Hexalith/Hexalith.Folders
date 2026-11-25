using Bunit;

using Microsoft.AspNetCore.Components;

using Shouldly;

using Xunit;

// 'Folders' is also a namespace segment (Hexalith.Folders); alias the page type to disambiguate.
using FoldersPage = Hexalith.Folders.UI.Components.Pages.Folders;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.6 / AC #1 / AC #11 — the folder list / discovery entry renders within the console shell,
/// exposes the tenant scope + a read-only navigation affordance, and registers no mutation control.
/// </summary>
public sealed class FoldersPageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RendersConsolePageRootWithControlsEnabledOnlyDuringInteractiveRendering(bool interactive)
    {
        (BunitContext ctx, _, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        ctx.SetRendererInfo(interactive ? new RendererInfo("Server", true) : new RendererInfo("Static", false));
        IRenderedComponent<FoldersPage> rendered = ctx.Render<FoldersPage>();

        rendered.Find("[data-testid=\"console-page-folders-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
        rendered.Find("[data-testid=\"tenant-scope-banner\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"console-page-folders-id-input\"]").HasAttribute("disabled").ShouldBe(!interactive);
        rendered.Find("[data-testid=\"console-page-folders-open\"]").HasAttribute("disabled").ShouldBe(!interactive);
    }

    [Fact]
    public void Renders_NoMutationAffordances()
    {
        (BunitContext ctx, _, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        ctx.SetRendererInfo(new RendererInfo("Server", true));
        IRenderedComponent<FoldersPage> rendered = ctx.Render<FoldersPage>();

        rendered.ShouldHaveNoMutationAffordances();
    }
}
