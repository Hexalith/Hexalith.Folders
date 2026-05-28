using Bunit;

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
    [Fact]
    public void RendersConsolePageRoot_WithSingleHeading()
    {
        (BunitContext ctx, _, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        IRenderedComponent<FoldersPage> rendered = ctx.Render<FoldersPage>();

        rendered.Find("[data-testid=\"console-page-folders-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
        rendered.Find("[data-testid=\"tenant-scope-banner\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"console-page-folders-id-input\"]").ShouldNotBeNull();
    }

    [Fact]
    public void Renders_NoMutationAffordances()
    {
        (BunitContext ctx, _, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        IRenderedComponent<FoldersPage> rendered = ctx.Render<FoldersPage>();

        rendered.ShouldHaveNoMutationAffordances();
    }
}
