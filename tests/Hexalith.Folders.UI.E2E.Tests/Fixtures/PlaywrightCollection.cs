namespace Hexalith.Folders.UI.E2E.Tests.Fixtures;

using Xunit;

/// <summary>
/// xUnit collection that shares a single <see cref="PlaywrightFixture"/>
/// across all UI E2E test classes, ensuring one browser launch per test run.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "Playwright";
}
