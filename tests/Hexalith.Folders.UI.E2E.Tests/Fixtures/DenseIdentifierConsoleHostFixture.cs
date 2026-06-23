namespace Hexalith.Folders.UI.E2E.Tests.Fixtures;

/// <summary>
/// Story 8.4 — the UX-DR31 dense-identifier populated console host for the zoom (125/150/200 %) /
/// no-horizontal-clipping invariant (Task 5). Seeds the stub
/// <see cref="Hexalith.Folders.Client.Generated.IClient"/> with long folder IDs / long paths / dense
/// identifiers across the tables, timelines, metadata trees, and trust summaries so the reflow stress is
/// exercised against realistic operational shapes — still synthetic, still metadata-only.
/// </summary>
public sealed class DenseIdentifierConsoleHostFixture : PopulatedConsoleHostFixture
{
    /// <inheritdoc />
    protected override ConsoleStubFixtures.Density Density => ConsoleStubFixtures.Density.Dense;
}
