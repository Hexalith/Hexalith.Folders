namespace Hexalith.Folders.ServiceDefaults;

/// <summary>
/// Bounded, metadata-only readiness inputs for the I-7 monitored snapshots aggregated by the
/// <see cref="MonitoredSnapshotReadinessCheck"/>: Dapr sidecar health, the Tenants-availability
/// degraded-mode active flag, and projection lag measured against the pinned C2 target.
/// </summary>
/// <remarks>
/// Projection lag is clock-derived and lives only in health/telemetry; it must never be baked into
/// replayable projection state (read-model determinism excludes clock-derived fields).
/// </remarks>
public sealed record ReadinessSnapshotState
{
    /// <summary>Whether the Dapr sidecar backing this service reports healthy.</summary>
    public bool DaprSidecarHealthy { get; init; } = true;

    /// <summary>Whether Tenants-availability degraded mode is currently active.</summary>
    public bool TenantsAvailabilityDegradedModeActive { get; init; }

    /// <summary>Observed projection lag in milliseconds (clock-derived, never persisted to projection state).</summary>
    public long ProjectionLagMilliseconds { get; init; }
}
