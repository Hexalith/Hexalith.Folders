using Hexalith.Folders.Server;
using Hexalith.Folders.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class ServerSmokeTests
{
    [Fact]
    public void ServerModuleIsScaffoldOnly() => FoldersServerModule.Description.ShouldContain("server scaffold");

    [Fact]
    public void FullServerTestHostCompositionShouldValidateOnBuild()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Test",
        });
        builder.Host.UseDefaultServiceProvider(static options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();

        using WebApplication app = builder.Build();

        app.MapFoldersServerEndpoints();
    }
}
