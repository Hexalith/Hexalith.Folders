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
    /// <returns>A builder ready for pipeline composition and startup.</returns>
    internal static WebApplicationBuilder CreateBuilder()
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
        });
        builder.WebHost.UseTestServer();
        builder.AddFoldersServerHost();
        return builder;
    }
}
