using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Uses the explicitly supplied domain clock for manually constructed SDK coordinators.</summary>
internal sealed class OperationalStatusTimeProvider(IUtcClock clock) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => clock.UtcNow;
}
