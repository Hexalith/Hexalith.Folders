using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Extensions;
using Hexalith.Folders.Server;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

/// <summary>Exercises the extension through the same loader used by composed gateways.</summary>
public sealed class FoldersEventStoreExtensionTests
{
    private const string PackageId = "Hexalith.Folders.EventStoreExtensions";
    private const string HealthCheckName = EventStoreExtensionServiceCollectionExtensions.HealthCheckName;

    [Fact]
    public async Task DeclaredExtensionMakesTheHostReadyAndRegistersEveryCommandRouteOnce()
    {
        HostApplicationBuilder builder = CreateBuilder("1");
        builder.Services.AddEventStoreExtensions(builder.Configuration);
        using IHost host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        string[] declaredRoutes = FoldersRuntimeCatalogDeclarations.Commands.Select(command => command.Route.RouteId).ToArray();
        var adapters = host.Services.GetServices<IIdempotencyIntentAdapter>();
        Dictionary<string, int> registrations = new(StringComparer.Ordinal);
        foreach (IIdempotencyIntentAdapter adapter in adapters)
        {
            string routeId = "folders-" + adapter.OperationId;
            declaredRoutes.ShouldContain(routeId, $"Missing declaration for '{routeId}'");
            adapter.ShouldBeAssignableTo<IDeclaredIdempotencyIntentAdapter>().Declaration.Route.RouteId
                .ShouldBe(routeId);
            registrations[routeId] = registrations.GetValueOrDefault(routeId) + 1;
        }

        foreach (string routeId in declaredRoutes)
        {
            registrations.GetValueOrDefault(routeId).ShouldBe(1, $"Adapter registrations for '{routeId}'");
        }

        EventStoreLoadedExtension extension = host.Services.GetRequiredService<EventStoreExtensionCatalog>().Extensions.ShouldHaveSingleItem();
        extension.PackageId.ShouldBe(PackageId);
        extension.AdapterCount.ShouldBe(declaredRoutes.Length);
        HealthReport health = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(
            check => check.Tags.Contains("ready"), TestContext.Current.CancellationToken);
        health.Entries[HealthCheckName].Status.ShouldBe(HealthStatus.Healthy);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void UnsupportedMajorRefusesThePackageBeforeTheHostCanBecomeReady()
    {
        HostApplicationBuilder builder = CreateBuilder("2");
        EventStoreExtensionException exception = Should.Throw<EventStoreExtensionException>(
            () => builder.Services.AddEventStoreExtensions(builder.Configuration));

        exception.Message.ShouldContain(PackageId);
        exception.Condition.ShouldBe(EventStoreExtensionConditions.UnsupportedMajor);
        builder.Services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(EventStoreExtensionCatalog));
        builder.Services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(IIdempotencyIntentAdapter));
    }

    [Fact]
    public void ManifestAndExtensionAssemblyDeclareTheSameProviderFreeIdentity()
    {
        string manifest = Path.Combine(AppContext.BaseDirectory, "hexalith.module-manifest.v2.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(manifest));
        JsonElement module = document.RootElement.GetProperty("modules").EnumerateArray().ShouldHaveSingleItem();
        JsonElement extension = module.GetProperty("runtime").GetProperty("eventStoreExtensions").EnumerateArray().ShouldHaveSingleItem();
        Assembly assembly = typeof(FoldersCatalogAdapterSource).Assembly;
        var identity = assembly.GetCustomAttribute<EventStoreExtensionAttribute>().ShouldNotBeNull();

        extension.GetProperty("packageId").GetString().ShouldBe(assembly.GetName().Name);
        extension.GetProperty("extensionId").GetString().ShouldBe(identity.Id);
        extension.GetProperty("extensionApiMajor").GetInt32().ShouldBe(identity.Major);
        extension.GetProperty("configurationInputs").EnumerateArray().ShouldBeEmpty();
        extension.TryGetProperty("providerException", out _).ShouldBeFalse();
    }

    private static HostApplicationBuilder CreateBuilder(string major)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        const string entry = EventStoreExtensionServiceCollectionExtensions.ConfigurationSection + ":0:";
        builder.Configuration[entry + "PackageId"] = PackageId;
        builder.Configuration[entry + "ExtensionId"] = "folders";
        builder.Configuration[entry + "ApiMajor"] = major;
        builder.Services.AddLogging();
        return builder;
    }
}
