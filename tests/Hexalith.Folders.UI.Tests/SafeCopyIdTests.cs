using Bunit;

using Hexalith.Folders.UI.Components;

using Microsoft.JSInterop;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>Verifies clipboard failures preserve the displayed read-only identifier.</summary>
public sealed class SafeCopyIdTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClipboardFailure_KeepsIdentifierAvailable(bool disconnected)
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();
        ctx.JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true)
            .SetException<Exception>(disconnected ? new JSDisconnectedException("Circuit closed.") : new JSException("Clipboard denied."));
        IRenderedComponent<SafeCopyId> rendered = ctx.Render<SafeCopyId>(p => p.Add(c => c.Value, "folder-1"));

        rendered.Find("[data-testid=\"safe-copy-button\"]").Click();

        rendered.Find("code").TextContent.ShouldBe("folder-1");
        rendered.ShouldHaveNoMutationAffordances();
    }
}
