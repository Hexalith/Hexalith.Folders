using System.Security.Cryptography;

using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Registration;
using Hexalith.Folders.Authorization;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Checks the selected owner registration through the actual SDK Development bootstrap options.</summary>
public sealed class FoldersDevelopmentOwnerMetadataTests
{
    /// <summary>The explicit private flag selects diagnostics without inventing raw SDK option configuration or issuing authority.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RealBootstrapOptionsSelectOnlyTheOptedInOwnerDecorator(bool diagnostics, bool loggerFails)
    {
        string directory = Directory.CreateTempSubdirectory("folder-owner-diagnostic-").FullName;
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.Configuration["Folders:EventStore:OwnerMetadataReads:Enabled"] = "true";
            builder.Configuration["Folders:EventStore:OwnerMetadataReads:DiagnosticsEnabled"] = diagnostics.ToString();
            var section = builder.Configuration.GetSection("EventStore:RouteCatalog:DevelopmentBootstrap");
            section["Directory"] = directory;
            section["HostId"] = "folders-host";
            section["CoordinatorHostId"] = "eventstore-host";
            section["AttemptId"] = "folder-owner-diagnostic";
            section["BindingVersion"] = "2";
            section["RequiredHostIds:0"] = "folders-host";
            section["RequiredHostIds:1"] = "eventstore-host";
            using RSA key = RSA.Create(2048);
            builder.Configuration["Authentication:BoundStreamRead:PrivateKey"] = Convert.ToBase64String(key.ExportPkcs8PrivateKey());
            builder.Services.AddSingleton<IWorkloadAssertionIssuer, DiagnosticCompositionAssertionIssuer>();
            builder.Services.AddFoldersServer();
            ServiceDescriptor original = builder.Services.Single(descriptor => descriptor.ServiceType == typeof(INativeSourceMetadataReader));
            original.Lifetime.ShouldBe(ServiceLifetime.Transient);
            var originalFactory = original.ImplementationFactory!;
            int factoryCalls = 0;
            builder.Services[builder.Services.IndexOf(original)] = ServiceDescriptor.Describe(typeof(INativeSourceMetadataReader), provider =>
            {
                factoryCalls++;
                return originalFactory(provider);
            }, original.Lifetime);
            if (loggerFails) builder.Services.AddSingleton<ILogger<FoldersDevelopmentOwnerMetadataDiagnostics>>(_ => throw new InvalidOperationException("Controlled optional logger failure."));
            builder.Services.AddEventStoreDevelopmentRouteCatalogBootstrap(builder.Configuration,
                (_, _) => throw new InvalidOperationException("Composition must not activate or observe a catalog."));
#if DEBUG && FOLDERS_OWNER_METADATA_SOURCE
            int serviceCount = builder.Services.Count;
            builder.AddFoldersDevelopmentOwnerMetadata();
            builder.Services.Count.ShouldBe(serviceCount);
            using var provider = builder.Services.BuildServiceProvider();

            builder.Configuration["EventStore:RouteCatalog:DevelopmentFixture"].ShouldBeNull();
            builder.Configuration["EventStore:RouteCatalog:Profile"].ShouldBeNull();
            builder.Configuration["EventStore:RouteCatalog:RequireTechnicalHttpBinding"].ShouldBeNull();
            INativeSourceMetadataReader reader = provider.GetRequiredService<INativeSourceMetadataReader>();
            if (diagnostics && !loggerFails) reader.ShouldBeOfType<FoldersDevelopmentOwnerMetadataDiagnostics>();
            else reader.ShouldBeOfType<GatewayNativeSourceMetadataReader>();
            provider.GetRequiredService<INativeSourceMetadataReader>().ShouldNotBeSameAs(reader);
            factoryCalls.ShouldBe(2);
            builder.Services.Single(descriptor => descriptor.ServiceType == typeof(INativeSourceMetadataReader))
                .Lifetime.ShouldBe(original.Lifetime);
            provider.GetRequiredService<IOptions<EventStoreGatewayClientOptions>>().Value.UseWorkloadTransport.ShouldBeFalse();
            provider.GetService<IEventStoreGatewayWorkloadAssertionSource>().ShouldBeNull();
            provider.GetService<IEventStoreGatewayStreamReadProofSource>().ShouldBeNull();
            provider.GetRequiredService<IOrganizationStreamSequenceReader>().ShouldBeOfType<GatewayOrganizationStreamSequenceReader>();
#else
            Should.Throw<InvalidOperationException>(() => builder.AddFoldersDevelopmentOwnerMetadata()).Message
                .ShouldBe("Private owner metadata requires a local Debug source build.");
#endif
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Disabled composition leaves the exact public descriptor and all registrations unchanged.</summary>
    [Fact]
    public void DisabledSelectionPreservesThePublicFactory()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.Configuration["Folders:EventStore:OwnerMetadataReads:Enabled"] = "false";
        builder.Services.AddFoldersServer();
        ServiceDescriptor original = builder.Services.Single(descriptor => descriptor.ServiceType == typeof(INativeSourceMetadataReader));
        int count = builder.Services.Count;
        builder.AddFoldersDevelopmentOwnerMetadata();
        builder.Services.Count.ShouldBe(count);
        builder.Services.Single(descriptor => descriptor.ServiceType == typeof(INativeSourceMetadataReader)).ShouldBeSameAs(original);
    }

    /// <summary>The explicit flag never admits production, staging or a missing Development catalog directory.</summary>
    [Theory]
    [InlineData("Production", "configured")]
    [InlineData("Staging", "configured")]
    [InlineData("Development", "")]
    public void EnabledSelectionRetainsEnvironmentAndCatalogGuards(string environment, string directory)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration["Folders:EventStore:OwnerMetadataReads:Enabled"] = "true";
        builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = directory;
        builder.Services.AddFoldersServer();
        ServiceDescriptor original = builder.Services.Single(descriptor => descriptor.ServiceType == typeof(INativeSourceMetadataReader));
        Should.Throw<InvalidOperationException>(() => builder.AddFoldersDevelopmentOwnerMetadata());
        builder.Services.Single(descriptor => descriptor.ServiceType == typeof(INativeSourceMetadataReader)).ShouldBeSameAs(original);
    }

#if DEBUG && FOLDERS_OWNER_METADATA_SOURCE
    /// <summary>Unexpected lifetimes, ambiguous registrations and missing factories refuse before any factory is invoked.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("singleton")]
    [InlineData("scoped")]
    [InlineData("instance")]
    [InlineData("keyed")]
    public void EnabledSelectionRequiresTheSinglePublicTransientFactory(string scenario)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration["Folders:EventStore:OwnerMetadataReads:Enabled"] = "true";
        builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = "configured";
        int calls = 0;
        object Factory(IServiceProvider _) { calls++; throw new InvalidOperationException("Unexpected resolution."); }
        switch (scenario)
        {
            case "duplicate":
                builder.Services.Add(ServiceDescriptor.Transient(typeof(INativeSourceMetadataReader), Factory));
                builder.Services.Add(ServiceDescriptor.Transient(typeof(INativeSourceMetadataReader), Factory));
                break;
            case "singleton": builder.Services.Add(ServiceDescriptor.Singleton(typeof(INativeSourceMetadataReader), Factory)); break;
            case "scoped": builder.Services.Add(ServiceDescriptor.Scoped(typeof(INativeSourceMetadataReader), Factory)); break;
            case "instance": builder.Services.AddSingleton(SourceHeadBoundaryProxy.Create<INativeSourceMetadataReader>((_, _) => throw new InvalidOperationException("No read permitted."))); break;
            case "keyed": builder.Services.AddKeyedTransient<INativeSourceMetadataReader>("unexpected", (_, _) => { calls++; throw new InvalidOperationException("Unexpected resolution."); }); break;
        }
        Should.Throw<InvalidOperationException>(() => builder.AddFoldersDevelopmentOwnerMetadata()).Message
            .ShouldBe("Private owner metadata requires the existing public SDK reader factory.");
        calls.ShouldBe(0);
    }

    /// <summary>A factory returning another reader cannot substitute a private transport for the public SDK adapter.</summary>
    [Fact]
    public void UnexpectedFactoryResultRefusesWithoutReadingOrIssuingAuthority()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration["Folders:EventStore:OwnerMetadataReads:Enabled"] = "true";
        builder.Configuration["Folders:EventStore:OwnerMetadataReads:DiagnosticsEnabled"] = "false";
        builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = "configured";
        var unexpected = SourceHeadBoundaryProxy.Create<INativeSourceMetadataReader>((_, _) => throw new InvalidOperationException("No read permitted."));
        builder.Services.AddTransient(_ => unexpected);
        builder.AddFoldersDevelopmentOwnerMetadata();
        using var provider = builder.Services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<INativeSourceMetadataReader>()).Message
            .ShouldBe("Private owner metadata requires the actual public SDK reader.");
        ((SourceHeadBoundaryProxy)(object)unexpected).Calls.ShouldBe(0);
    }
#endif

}
