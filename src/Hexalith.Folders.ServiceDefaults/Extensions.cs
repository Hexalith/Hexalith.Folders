using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Hexalith.Folders.ServiceDefaults;

public static class Extensions
{
    private const string ServiceName = "Hexalith.Folders";
    private const string ActivitySourceName = "Hexalith.Folders.Observability";
    private const string MeterName = "Hexalith.Folders.Observability";

    /// <summary>Health-check tag selecting the liveness probe surfaced at <c>/health/live</c>.</summary>
    public const string LivenessTag = "live";

    /// <summary>Health-check tag selecting the readiness probe surfaced at <c>/health/ready</c>.</summary>
    public const string ReadinessTag = "ready";

    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(static http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        bool otlpConfigured = IsOtlpConfigured(builder.Configuration);

        // I-6 third signal: structured logs are bridged through OpenTelemetry and exported via the same
        // OTLP env seam as metrics/traces, so production exporters (Jaeger / Tempo / App Insights / Datadog)
        // stay pluggable and vendor-neutral. Until 7.12 only metrics and traces were exported.
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ServiceName));

            if (otlpConfigured)
            {
                logging.AddOtlpExporter();
            }
        });

        IOpenTelemetryBuilder telemetry = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName));

        telemetry.WithMetrics(metrics =>
        {
            metrics
                .AddMeter(MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();

            if (otlpConfigured)
            {
                metrics.AddOtlpExporter();
            }
        });

        telemetry.WithTracing(tracing =>
        {
            tracing
                .AddSource(ActivitySourceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();

            if (otlpConfigured)
            {
                tracing.AddOtlpExporter();
            }
        });

        return builder;
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // I-7 monitored-snapshot readiness. A host may register a richer IReadinessSnapshotSource; the
        // default keeps readiness serving so the probe is never vacuous.
        builder.Services.TryAddSingleton<IReadinessSnapshotSource, HealthyReadinessSnapshotSource>();
        builder.Services
            .AddHealthChecks()
            .AddCheck("self", static () => HealthCheckResult.Healthy("alive"), tags: [LivenessTag])
            .AddCheck<MonitoredSnapshotReadinessCheck>("monitored-snapshots", tags: [ReadinessTag]);

        return builder;
    }

    public static IEndpointRouteBuilder MapDefaultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // I-7 liveness/readiness split. Readiness reports HealthStatus.Degraded ("degraded-but-serving",
        // still HTTP 200) when projection lag exceeds the pinned C2 target instead of failing readiness.
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = static registration => registration.Tags.Contains(LivenessTag),
        });
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = static registration => registration.Tags.Contains(ReadinessTag),
        });

        // Compatibility alias for the pre-7.12 flat liveness probe.
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = static registration => registration.Tags.Contains(LivenessTag),
        });

        return endpoints;
    }

    private static bool IsOtlpConfigured(IConfiguration configuration)
        => !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
}
