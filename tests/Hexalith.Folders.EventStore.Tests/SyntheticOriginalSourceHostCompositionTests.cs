using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

/// <summary>Loads the actual Folders command receiver entry point, including its explicitly selected owner append worker.</summary>
[Collection("OriginalSourceHostComposition")]
public sealed class SyntheticOriginalSourceHostCompositionTests
{
    /// <summary>The normal actual command host registers no synthetic source or append worker.</summary>
    [Fact]
    public void ActualHostDefaultsToNativeActorAuthority()
    {
        using var host = new FoldersEventStoreHostFactory();
        host.Services.GetServices<INativeSourceScanSource>().ShouldBeEmpty();
        host.Services.GetServices<IHostedService>().ShouldNotContain(service => service.GetType().Name == "SyntheticOriginalSourceWorker");
    }

#if PRIVATE_ORIGINAL_SOURCE_PROOF
    /// <summary>The actual private command host starts its owner worker and independently commits selected originals.</summary>
    [Fact]
    public async Task ActualPrivateHostStartsOwnerAppendWorker()
    {
        const string run = "0123456789abcdef";
        const string tenant = "durability-" + run;
        string directory = Path.Combine(Path.GetTempPath(), "folders-original-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var selection = new Dictionary<string, string?>
        {
            ["Authentication__WorkloadIssuer__Workload"] = "eventstore-" + run,
            ["EventStore__Testing__OriginalSource__Enabled"] = "true",
            ["EventStore__Testing__OriginalSource__Mode"] = "synthetic-original-primary-v1",
            ["EventStore__Testing__OriginalSource__RunId"] = run,
            ["EventStore__Testing__OriginalSource__Directory"] = directory,
            ["EventStore__Testing__OriginalSource__Tenant"] = tenant,
            ["EventStore__Testing__OriginalSource__Domain"] = "folders",
            ["EventStore__Testing__OriginalSource__AggregatePrefix"] = "capacity-",
        };
        var originalEnvironment = selection.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            // Match actual Aspire process launch: startup selection exists before CreateBuilder.
            // WebApplicationFactory's app-configuration callback runs after early registrations.
            foreach (var setting in selection) Environment.SetEnvironmentVariable(setting.Key, setting.Value);
            using var baseline = new FoldersEventStoreHostFactory();
            await using var host = baseline.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services => services.Configure<RouteCatalogHostOptions>(options =>
                { options.DevelopmentFixture = true; options.Profile = "Development"; options.RequireTechnicalHttpBinding = true; }));
            });
            var source = host.Services.GetServices<INativeSourceScanSource>().ShouldHaveSingleItem();
            host.Services.GetServices<IHostedService>().ShouldContain(service => service.GetType().Name == "SyntheticOriginalSourceWorker");
            var client = new Hexalith.EventStore.Testing.OriginalSource.SyntheticOriginalSourceClient(directory, run);
            ProjectionEventDto original = new("Original.Event", "{}"u8.ToArray(), "json", 1,
                DateTimeOffset.UnixEpoch, "original-correlation", "original-message", GlobalPosition: 7) { MetadataVersion = 1 };
            (await client.AppendAsync(tenant, "folders", "capacity-host", [original], TestContext.Current.CancellationToken)).ShouldBeTrue();
            StreamReadPage page = await source.ReadAsync(new(tenant, "folders", "capacity-host", PageSize: 255) { RequireNativeProof = true }, TestContext.Current.CancellationToken);
            page.Events.ShouldHaveSingleItem().Payload.ShouldBe(original.Payload);
            page.Metadata.NativeProof!.HeadSequence.ShouldBe(1);
            source.IsSelected(new AggregateIdentity(tenant, "folders", "ordinary-folder")).ShouldBeFalse();
        }
        finally
        {
            foreach (var setting in originalEnvironment) Environment.SetEnvironmentVariable(setting.Key, setting.Value);
            Directory.Delete(directory, recursive: true);
        }
    }
#endif
}
