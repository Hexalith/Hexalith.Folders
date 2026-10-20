using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Tests.OperationalStatus;

internal sealed class OperationalStatusClock : TimeProvider, IUtcClock
{
    public DateTimeOffset UtcNow { get; set; } = OperationalStatusTestData.Now.AddHours(1);

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
