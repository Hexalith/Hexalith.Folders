using Bunit;

using Hexalith.Folders.UI.Components;
using Hexalith.Folders.UI.Components.Models;
using Hexalith.Folders.UI.Services;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

public sealed class ConsoleErrorPanelTests
{
    [Theory]
    [InlineData("tenant_access_denied", ConsoleErrorDisposition.Denied, "denied", "Access denied")]
    [InlineData("read_model_unavailable", ConsoleErrorDisposition.AuthorityUnavailable, "authorityunavailable", "Authority temporarily unavailable")]
    [InlineData("projection_stale", ConsoleErrorDisposition.AuthorityUnavailable, "authorityunavailable", "Authority temporarily unavailable")]
    [InlineData("projection_unavailable", ConsoleErrorDisposition.AuthorityUnavailable, "authorityunavailable", "Authority temporarily unavailable")]
    public void RendersDistinctDispositionInTheDom(
        string reasonToken,
        ConsoleErrorDisposition disposition,
        string expectedAttribute,
        string expectedLabel)
    {
        (BunitContext context, _, _) = DiagnosticTestContext.Create();
        using BunitContext _context = context;
        ConsoleErrorView error = new(
            reasonToken,
            "Safe explanation.",
            "correlation_01HZY7Z6N7J4Q2X8Y9V0A1B2C3",
            Retryable: true,
            ClientAction: "retry",
            disposition);

        IRenderedComponent<ConsoleErrorPanel> rendered = context.Render<ConsoleErrorPanel>(parameters =>
            parameters.Add(component => component.Error, error));

        rendered.Find("[data-testid='console-error-panel']")
            .GetAttribute("data-fc-error-disposition").ShouldBe(expectedAttribute);
        rendered.Find("[data-testid='console-error-disposition']").TextContent.ShouldContain(expectedLabel);
    }
}
