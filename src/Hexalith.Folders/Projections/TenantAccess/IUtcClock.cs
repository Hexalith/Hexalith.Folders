namespace Hexalith.Folders.Projections.TenantAccess;

public interface IUtcClock
{
    DateTimeOffset UtcNow { get; }
}
