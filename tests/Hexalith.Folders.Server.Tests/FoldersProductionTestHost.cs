using Hexalith.Folders.Server;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Creates a production-composed host with isolated, non-secret OIDC settings.</summary>
internal static class FoldersProductionTestHost
{
    /// <summary>Creates the actual production service graph without an external listener.</summary>
    /// <param name="settings">Optional non-secret settings applied before actual host registration.</param>
    /// <returns>A builder ready for pipeline composition and startup.</returns>
    internal static WebApplicationBuilder CreateBuilder(IReadOnlyDictionary<string, string?>? settings = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Authority"] = "https://identity.example.test/realms/folders",
            ["Authentication:JwtBearer:Issuer"] = "https://identity.example.test/realms/folders",
            ["Authentication:JwtBearer:Audience"] = "folders",
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "RS256",
            [DaprAppChannelToken.ConfigurationKey] = "test-sidecar-token",
        });
        if (settings is not null) builder.Configuration.AddInMemoryCollection(settings);
        builder.WebHost.UseTestServer();
        builder.AddFoldersServerHost();
        builder.Services.Configure<RouteCatalogHostOptions>(options => options.Identity = new("folders", "folders",
            Convert.ToHexStringLower(SHA256.HashData("folders-source-fixture-inventory"u8)),
            Convert.ToHexStringLower(SHA256.HashData("isolated-test-server"u8)),
            Convert.ToHexStringLower(SHA256.HashData("rsa-workload-callback-fixture"u8))));
        builder.Services.AddSingleton<FoldersCallbackRouteCatalog>(services => new(services,
            () => services.GetRequiredService<EventStoreDomainServiceEndpointSource>().Endpoints!.DataSources
                .SelectMany(source => source.Endpoints)));
        builder.Services.AddSingleton<IRouteCatalogAuthority>(services => services.GetRequiredService<FoldersCallbackRouteCatalog>());
        return builder;
    }
}
