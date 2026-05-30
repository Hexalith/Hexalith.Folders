namespace Hexalith.Folders.ServiceDefaults;

/// <summary>
/// Supplies the current I-7 monitored-snapshot readiness inputs to <see cref="MonitoredSnapshotReadinessCheck"/>.
/// Hosts can register a richer source; the default reports a healthy, serving baseline.
/// </summary>
public interface IReadinessSnapshotSource
{
    /// <summary>Captures the current bounded readiness snapshot.</summary>
    ReadinessSnapshotState Capture();
}
