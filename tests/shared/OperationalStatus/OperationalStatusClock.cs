using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Tests.OperationalStatus;

internal sealed class OperationalStatusClock : IUtcClock
{
    public DateTimeOffset UtcNow { get; set; } = OperationalStatusTestData.Now.AddHours(1);
}
