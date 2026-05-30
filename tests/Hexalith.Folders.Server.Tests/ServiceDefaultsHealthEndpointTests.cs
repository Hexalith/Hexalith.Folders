using System.Net;

using Hexalith.Folders.ServiceDefaults;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class ServiceDefaultsHealthEndpointTests
{
    [Fact]
    public async Task ReadinessCheckShouldReportHealthyWhenSnapshotsStayInsideC2Budget()
    {
        HealthCheckResult result = await CheckAsync(new ReadinessSnapshotState
        {
            ProjectionLagMilliseconds = MonitoredSnapshotReadinessCheck.C2ProjectionLagBudgetMilliseconds,
        }).ConfigureAwait(true);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldBe("ready");
        result.Data[MonitoredSnapshotReadinessCheck.DaprSidecarSnapshot].ShouldBe(true);
        result.Data[MonitoredSnapshotReadinessCheck.TenantsAvailabilitySnapshot].ShouldBe(false);
        result.Data[MonitoredSnapshotReadinessCheck.ProjectionLagSnapshot].ShouldBe(true);
    }

    [Theory]
    [InlineData(MonitoredSnapshotReadinessCheck.C2ProjectionLagBudgetMilliseconds + 1)]
    [InlineData(MonitoredSnapshotReadinessCheck.C2ProjectionLagBudgetMilliseconds * 2)]
    public async Task ReadinessCheckShouldReportDegradedButServingWhenProjectionLagExceedsC2Budget(long lagMilliseconds)
    {
        HealthCheckResult result = await CheckAsync(new ReadinessSnapshotState
        {
            ProjectionLagMilliseconds = lagMilliseconds,
        }).ConfigureAwait(true);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldBe(MonitoredSnapshotReadinessCheck.DegradedButServingDescription);
        result.Data[MonitoredSnapshotReadinessCheck.DaprSidecarSnapshot].ShouldBe(true);
        result.Data[MonitoredSnapshotReadinessCheck.TenantsAvailabilitySnapshot].ShouldBe(false);
        result.Data[MonitoredSnapshotReadinessCheck.ProjectionLagSnapshot].ShouldBe(false);
    }

    [Fact]
    public async Task ReadinessCheckShouldReportDegradedButServingWhenTenantsAvailabilityIsDegraded()
    {
        HealthCheckResult result = await CheckAsync(new ReadinessSnapshotState
        {
            TenantsAvailabilityDegradedModeActive = true,
            ProjectionLagMilliseconds = 1,
        }).ConfigureAwait(true);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldBe(MonitoredSnapshotReadinessCheck.DegradedButServingDescription);
        result.Data[MonitoredSnapshotReadinessCheck.DaprSidecarSnapshot].ShouldBe(true);
        result.Data[MonitoredSnapshotReadinessCheck.TenantsAvailabilitySnapshot].ShouldBe(true);
        result.Data[MonitoredSnapshotReadinessCheck.ProjectionLagSnapshot].ShouldBe(true);
    }

    [Fact]
    public async Task ReadinessCheckShouldReportUnhealthyWhenDaprSidecarIsUnavailable()
    {
        HealthCheckResult result = await CheckAsync(new ReadinessSnapshotState
        {
            DaprSidecarHealthy = false,
            ProjectionLagMilliseconds = 1,
        }).ConfigureAwait(true);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldBe("dapr_sidecar_unavailable");
        result.Data[MonitoredSnapshotReadinessCheck.DaprSidecarSnapshot].ShouldBe(false);
        result.Data[MonitoredSnapshotReadinessCheck.TenantsAvailabilitySnapshot].ShouldBe(false);
        result.Data[MonitoredSnapshotReadinessCheck.ProjectionLagSnapshot].ShouldBe(true);
    }

    [Fact]
    public async Task DefaultHealthEndpointsShouldExposeLivenessReadinessAndCompatibilityAlias()
    {
        using WebApplication app = await StartAppAsync(new FixedReadinessSnapshotSource(new ReadinessSnapshotState()))
            .ConfigureAwait(true);
        HttpClient client = app.GetTestClient();

        HttpResponseMessage live = await client
            .GetAsync("/health/live", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        HttpResponseMessage alias = await client
            .GetAsync("/health", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        HttpResponseMessage ready = await client
            .GetAsync("/health/ready", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        alias.StatusCode.ShouldBe(HttpStatusCode.OK);
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadinessEndpointShouldReturnOkForDegradedButServingSnapshots()
    {
        using WebApplication app = await StartAppAsync(new FixedReadinessSnapshotSource(new ReadinessSnapshotState
        {
            ProjectionLagMilliseconds = MonitoredSnapshotReadinessCheck.C2ProjectionLagBudgetMilliseconds + 1,
        })).ConfigureAwait(true);

        HttpResponseMessage response = await app.GetTestClient()
            .GetAsync("/health/ready", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadinessEndpointShouldReturnUnavailableWhenDaprSidecarIsUnavailable()
    {
        using WebApplication app = await StartAppAsync(new FixedReadinessSnapshotSource(new ReadinessSnapshotState
        {
            DaprSidecarHealthy = false,
        })).ConfigureAwait(true);

        HttpResponseMessage response = await app.GetTestClient()
            .GetAsync("/health/ready", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    private static async Task<HealthCheckResult> CheckAsync(ReadinessSnapshotState snapshot)
    {
        MonitoredSnapshotReadinessCheck check = new(new FixedReadinessSnapshotSource(snapshot));
        return await check
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
    }

    private static async Task<WebApplication> StartAppAsync(IReadinessSnapshotSource source)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(source);
        builder.AddDefaultHealthChecks();

        WebApplication app = builder.Build();
        app.MapDefaultEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return app;
    }

    private sealed class FixedReadinessSnapshotSource(ReadinessSnapshotState snapshot) : IReadinessSnapshotSource
    {
        public ReadinessSnapshotState Capture() => snapshot;
    }
}
