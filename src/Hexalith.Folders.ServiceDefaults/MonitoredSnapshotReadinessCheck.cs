using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.Folders.ServiceDefaults;

/// <summary>
/// I-7 readiness health check aggregating the monitored snapshots: Dapr sidecar health, the
/// Tenants-availability degraded-mode active flag, and projection lag versus the pinned C2 target.
/// Reports <see cref="HealthStatus.Degraded"/> ("degraded-but-serving", HTTP 200) when projection
/// lag exceeds C2 rather than failing readiness outright. Observe-only: it never mutates state.
/// </summary>
public sealed class MonitoredSnapshotReadinessCheck(IReadinessSnapshotSource source) : IHealthCheck
{
    /// <summary>
    /// C2 status-freshness ceiling in milliseconds, pinned in <c>docs/exit-criteria/c2-freshness.md</c>.
    /// Projection lag above this threshold reports degraded-but-serving, not unhealthy.
    /// </summary>
    public const int C2ProjectionLagBudgetMilliseconds = 500;

    /// <summary>Monitored-snapshot key: Dapr sidecar health.</summary>
    public const string DaprSidecarSnapshot = "dapr_sidecar_health";

    /// <summary>Monitored-snapshot key: Tenants-availability degraded-mode active flag.</summary>
    public const string TenantsAvailabilitySnapshot = "tenants_availability_degraded_mode";

    /// <summary>Monitored-snapshot key: projection lag versus the C2 target.</summary>
    public const string ProjectionLagSnapshot = "projection_lag";

    /// <summary>Readiness description emitted when projection lag exceeds C2 but the service still serves.</summary>
    public const string DegradedButServingDescription = "degraded-but-serving";

    private readonly IReadinessSnapshotSource _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        ReadinessSnapshotState snapshot = _source.Capture();

        // Bounded, metadata-only booleans only — never the raw clock-derived lag value.
        IReadOnlyDictionary<string, object> data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [DaprSidecarSnapshot] = snapshot.DaprSidecarHealthy,
            [TenantsAvailabilitySnapshot] = snapshot.TenantsAvailabilityDegradedModeActive,
            [ProjectionLagSnapshot] = snapshot.ProjectionLagMilliseconds <= C2ProjectionLagBudgetMilliseconds,
        };

        if (!snapshot.DaprSidecarHealthy)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("dapr_sidecar_unavailable", data: data));
        }

        if (snapshot.ProjectionLagMilliseconds > C2ProjectionLagBudgetMilliseconds
            || snapshot.TenantsAvailabilityDegradedModeActive)
        {
            return Task.FromResult(new HealthCheckResult(HealthStatus.Degraded, DegradedButServingDescription, data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("ready", data));
    }
}
