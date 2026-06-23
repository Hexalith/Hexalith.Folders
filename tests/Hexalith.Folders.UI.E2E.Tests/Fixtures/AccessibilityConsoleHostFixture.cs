namespace Hexalith.Folders.UI.E2E.Tests.Fixtures;

/// <summary>
/// Story 8.4 — the happy-path populated console host for the axe / WCAG 2.2 AA scan (Task 3) and the
/// keyboard-navigation / visible-focus assertions (Task 4). Seeds the stub <see cref="Hexalith.Folders.Client.Generated.IClient"/>
/// with the conventional short synthetic identifiers so every journey page renders its full read-only surface.
/// </summary>
public sealed class AccessibilityConsoleHostFixture : PopulatedConsoleHostFixture
{
    /// <inheritdoc />
    protected override ConsoleStubFixtures.Density Density => ConsoleStubFixtures.Density.HappyPath;
}
