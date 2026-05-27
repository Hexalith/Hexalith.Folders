using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Hexalith.Folders.ServiceDefaults;

public static class Extensions
{
    private const string ServiceName = "Hexalith.Folders";
    private const string ActivitySourceName = "Hexalith.Folders.Observability";
    private const string MeterName = "Hexalith.Folders.Observability";

    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(static http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
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

            if (IsOtlpConfigured(builder.Configuration))
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

            if (IsOtlpConfigured(builder.Configuration))
            {
                tracing.AddOtlpExporter();
            }
        });

        return builder;
    }

    public static IEndpointRouteBuilder MapDefaultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/health", static () => Results.Ok(new { status = "healthy" }));
        return endpoints;
    }

    private static bool IsOtlpConfigured(IConfiguration configuration)
        => !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
}
