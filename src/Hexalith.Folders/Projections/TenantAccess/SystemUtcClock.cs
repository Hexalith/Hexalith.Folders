namespace Hexalith.Folders.Projections.TenantAccess;

public sealed class SystemUtcClock : IUtcClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
