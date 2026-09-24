namespace Hexalith.Folders.IntegrationTests.Routing;

/// <summary>Pins time so seeded authorization and projection freshness stay current.</summary>
/// <param name="now">The fixed current instant.</param>
internal sealed class RoutingModeFixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => now;
}
