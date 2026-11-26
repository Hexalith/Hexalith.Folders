using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Server.Tests.Provisioning;

/// <summary>Advances lease time without making the authority fixture wait for wall time.</summary>
internal sealed class ProvisioningQualificationClock : TimeProvider, IUtcClock
{
    internal static readonly DateTimeOffset BeforeRestart = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);
    public DateTimeOffset UtcNow { get; set; } = BeforeRestart;
    public override DateTimeOffset GetUtcNow() => UtcNow;
}
