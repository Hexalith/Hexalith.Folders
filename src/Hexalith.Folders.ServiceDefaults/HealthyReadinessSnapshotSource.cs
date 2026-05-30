namespace Hexalith.Folders.ServiceDefaults;

/// <summary>
/// Default <see cref="IReadinessSnapshotSource"/> reporting a healthy, serving baseline so the
/// readiness probe is non-vacuous until a host registers richer I-7 snapshot contributors.
/// </summary>
public sealed class HealthyReadinessSnapshotSource : IReadinessSnapshotSource
{
    /// <inheritdoc />
    public ReadinessSnapshotState Capture() => new();
}
