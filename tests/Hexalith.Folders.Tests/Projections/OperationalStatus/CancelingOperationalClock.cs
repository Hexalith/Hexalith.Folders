using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Tests.OperationalStatus;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

/// <summary>Cancels when admitted history begins materializing its source-deadline operations.</summary>
internal sealed class CancelingOperationalClock(CancellationTokenSource cancellation, int cancelAt) : IUtcClock
{
    internal int Reads { get; private set; }
    public DateTimeOffset UtcNow
    {
        get
        {
            if (++Reads == cancelAt) cancellation.Cancel();
            return OperationalStatusTestData.Now.AddDays(1);
        }
    }
}
