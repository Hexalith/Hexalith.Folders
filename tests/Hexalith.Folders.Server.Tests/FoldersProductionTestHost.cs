using Hexalith.Folders.Server;

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
    /// <param name="environmentName">The explicit test environment, Production unless the supported catalog fixture is requested.</param>
    /// <returns>A builder ready for pipeline composition and startup.</returns>
    internal static WebApplicationBuilder CreateBuilder(string environmentName = "Production")
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName,
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Authority"] = "https://identity.example.test/realms/folders",
            ["Authentication:JwtBearer:Issuer"] = "https://identity.example.test/realms/folders",
            ["Authentication:JwtBearer:Audience"] = "folders",
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "RS256",
            ["APP_API_TOKEN"] = "production-fixture-channel",
        });
        builder.WebHost.UseTestServer();
        builder.AddFoldersServerHost();
        return builder;
    }
}
