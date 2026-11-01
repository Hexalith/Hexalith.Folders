using Hexalith.Folders.ServiceDefaults;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Supplies one bounded monitored snapshot to the actual production readiness pipeline.</summary>
/// <param name="snapshot">The observed dependency and projection state.</param>
internal sealed class HostReadinessSnapshotSource(ReadinessSnapshotState snapshot) : IReadinessSnapshotSource
{
    /// <inheritdoc/>
    public ReadinessSnapshotState Capture() => snapshot;
}
