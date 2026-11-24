namespace Hexalith.Folders.UI.E2E.Tests.Fixtures;

/// <summary>
/// The smoke console host with its static web assets served, so <c>_framework/blazor.web.js</c> loads and each page
/// starts a Blazor Server circuit. Use it for tests that drive server-handled interactions.
/// </summary>
public sealed class InteractiveConsoleHostFixture : AspireConsoleHostFixture
{
    /// <inheritdoc/>
    protected override bool ServeStaticWebAssets => true;
}
