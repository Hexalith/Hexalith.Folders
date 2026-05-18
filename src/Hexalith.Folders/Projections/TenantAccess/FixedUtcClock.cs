namespace Hexalith.Folders.Projections.TenantAccess;

public sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
