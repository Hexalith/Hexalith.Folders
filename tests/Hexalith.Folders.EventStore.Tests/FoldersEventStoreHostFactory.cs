using Hexalith.Folders.EventStore;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.EventStore.Tests;

/// <summary>Loads the real command-host entry point and security startup validation without sidecar background workers.</summary>
/// <param name="enableSignalR">Whether to include the production human-authenticated hub endpoints.</param>
internal sealed class FoldersEventStoreHostFactory(bool enableSignalR = false) : WebApplicationFactory<FoldersIdempotencyIntentAdapterCatalog>
{
    internal const string ChannelToken = "gateway-composition-test-channel";

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Authority"] = string.Empty,
                ["Authentication:JwtBearer:Issuer"] = "test-issuer",
                ["Authentication:JwtBearer:Audience"] = "eventstore",
                ["Authentication:JwtBearer:SigningKey"] = "test-signing-key-with-at-least-thirty-two-bytes",
                ["APP_API_TOKEN"] = ChannelToken,
                ["EventStore:SignalR:Enabled"] = enableSignalR ? "true" : "false",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty,
            }));
        // Preserve the SDK's actual HTTP/authorization startup enforcement. Only infrastructure
        // workers that require a live sidecar are outside this isolated production-pipeline fixture.
        builder.ConfigureServices(static services =>
        {
            foreach (ServiceDescriptor descriptor in services.Where(static descriptor => descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType?.Name is not ("EventStoreHttpHostSecurityStartupValidator" or "EventStoreAuthorizationStartupValidator")).ToArray())
                services.Remove(descriptor);
        });
    }
}
