using System.Security.Cryptography;

using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Checks the selected owner registration through the actual SDK Development bootstrap options.</summary>
public sealed class FoldersDevelopmentOwnerMetadataTests
{
    /// <summary>The explicit private flag selects diagnostics without inventing raw SDK option configuration or issuing authority.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealBootstrapOptionsSelectOnlyTheOptedInOwnerDecorator(bool diagnostics)
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
            builder.Services.AddEventStoreDevelopmentRouteCatalogBootstrap(builder.Configuration,
                (_, _) => throw new InvalidOperationException("Composition must not activate or observe a catalog."));
#if DEBUG && FOLDERS_OWNER_METADATA_SOURCE
            builder.AddFoldersDevelopmentOwnerMetadata();
            using var provider = builder.Services.BuildServiceProvider();

            builder.Configuration["EventStore:RouteCatalog:DevelopmentFixture"].ShouldBeNull();
            builder.Configuration["EventStore:RouteCatalog:Profile"].ShouldBeNull();
            builder.Configuration["EventStore:RouteCatalog:RequireTechnicalHttpBinding"].ShouldBeNull();
            INativeSourceMetadataReader reader = provider.GetRequiredService<INativeSourceMetadataReader>();
            if (diagnostics) reader.ShouldBeOfType<FoldersDevelopmentOwnerMetadataDiagnostics>();
            else reader.ShouldBeOfType<BoundOwnerNativeSourceMetadataReader>();
#else
            Should.Throw<InvalidOperationException>(() => builder.AddFoldersDevelopmentOwnerMetadata()).Message
                .ShouldBe("Private owner metadata requires a local Debug source build.");
#endif
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
