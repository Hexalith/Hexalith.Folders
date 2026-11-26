using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Extensions;
using Hexalith.Folders.Server;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
const string entry = EventStoreExtensionServiceCollectionExtensions.ConfigurationSection + ":0:";
builder.Configuration[entry + "PackageId"] = "Hexalith.Folders.EventStoreExtensions";
builder.Configuration[entry + "ExtensionId"] = "folders";
builder.Configuration[entry + "ApiMajor"] = "1";
builder.Services.AddLogging();
builder.Services.AddEventStoreExtensions(builder.Configuration);

using IHost host = builder.Build();
using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
await host.StartAsync(timeout.Token).ConfigureAwait(false);

string[] declaredRoutes = FoldersRuntimeCatalogDeclarations.Commands
    .Select(command => command.Route.RouteId)
    .Order(StringComparer.Ordinal)
    .ToArray();
string[] registeredRoutes = host.Services.GetServices<IIdempotencyIntentAdapter>()
    .Cast<IDeclaredIdempotencyIntentAdapter>()
    .Select(adapter => adapter.Declaration.Route.RouteId)
    .Order(StringComparer.Ordinal)
    .ToArray();
if (registeredRoutes.Length == 0
    || registeredRoutes.Distinct(StringComparer.Ordinal).Count() != registeredRoutes.Length
    || !registeredRoutes.SequenceEqual(declaredRoutes, StringComparer.Ordinal))
{
    throw new InvalidOperationException(
        $"Folders extension routes differ from the declared inventory. Expected [{string.Join(", ", declaredRoutes)}], "
        + $"registered [{string.Join(", ", registeredRoutes)}].");
}

HealthReport health = await host.Services.GetRequiredService<HealthCheckService>()
    .CheckHealthAsync(check => check.Tags.Contains("ready"), timeout.Token).ConfigureAwait(false);
HealthStatus status = health.Entries[EventStoreExtensionServiceCollectionExtensions.HealthCheckName].Status;
if (status != HealthStatus.Healthy)
{
    throw new InvalidOperationException($"The packaged Folders extension reported {status}.");
}

Console.WriteLine($"Packaged Folders extension registered {registeredRoutes.Length} unique routes and reported {status}.");
await host.StopAsync(timeout.Token).ConfigureAwait(false);
