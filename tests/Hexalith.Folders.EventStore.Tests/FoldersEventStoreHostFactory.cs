using Hexalith.Folders.EventStore;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.EventStore.Tests;

/// <summary>Loads the real command-host entry point while excluding its sidecar background workers.</summary>
internal sealed class FoldersEventStoreHostFactory : WebApplicationFactory<FoldersIdempotencyIntentAdapterCatalog>
{
    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration(static (_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Authority"] = string.Empty,
                ["Authentication:JwtBearer:Issuer"] = "test-issuer",
                ["Authentication:JwtBearer:Audience"] = "eventstore",
                ["Authentication:JwtBearer:SigningKey"] = "test-signing-key-with-at-least-thirty-two-bytes",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty,
            }));
        // These tests exercise Program's exception-handler registrations, not Dapr worker lifecycle.
        builder.ConfigureServices(static services => services.RemoveAll<IHostedService>());
    }
}
