namespace Hexalith.Folders.UI.E2E.Tests.Smoke;

using Xunit;

/// <summary>
/// Placeholder for the operations console smoke suite. Epic 6 (read-only
/// operations console) is the gating prerequisite; this test exists so the
/// project compiles and the lane is wired, but it deliberately does not run.
/// Replace with route + accessibility smoke once story 6-2 ships stable
/// selectors.
/// </summary>
public sealed class OperationsConsolePlaceholderSmokeTests
{
    [Fact(Skip = "Pending Epic 6 story 6-2 (FrontComposer-hosted read-only operations console). Replace with route and accessibility smoke once stable selectors exist.")]
    public Task PlaceholderConsoleHomePageLoads() => Task.CompletedTask;
}
