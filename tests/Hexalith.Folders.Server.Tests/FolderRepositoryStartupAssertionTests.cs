using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Server;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Proves production startup requires every actor-folded repository.</summary>
public sealed class FolderRepositoryStartupAssertionTests
{
    /// <summary>Starts the actual production graph and serves its liveness endpoint.</summary>
    [Fact]
    public async Task ProductionHostStartsWithActorFoldedRepositories()
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        app.Services.GetServices<IHostedService>().OfType<FolderRepositoryStartupAssertion>()
            .Count().ShouldBe(1);

        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpResponseMessage response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.EnsureSuccessStatusCode();
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Refuses production startup before traffic when a required repository is missing or volatile.</summary>
    /// <param name="repository">The repository registration to break.</param>
    /// <param name="memoryFallback">Whether to replace it with the existing volatile implementation.</param>
    /// <param name="message">The expected safe startup diagnostic.</param>
    [Theory]
    [InlineData("folder", false, "actor-folded folder repository")]
    [InlineData("folder", true, "actor-folded folder repository")]
    [InlineData("binding", false, "actor-folded organization repository")]
    [InlineData("binding", true, "actor-folded organization repository")]
    [InlineData("acl", false, "actor-folded organization ACL repository")]
    public async Task ProductionHostRefusesMissingOrMemoryRepository(string repository, bool memoryFallback, string message)
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        // Exercise the startup guard itself rather than DI's independent build-time missing-service check.
        builder.Host.UseDefaultServiceProvider(static options =>
        {
            options.ValidateOnBuild = false;
            options.ValidateScopes = true;
        });

        if (repository == "folder")
        {
            builder.Services.RemoveAll<IFolderRepository>();
            if (memoryFallback)
            {
                builder.Services.AddSingleton<IFolderRepository, InMemoryFolderRepository>();
            }
        }
        else if (repository == "binding")
        {
            builder.Services.RemoveAll<IOrganizationProviderBindingRepository>();
            if (memoryFallback)
            {
                builder.Services.AddSingleton<IOrganizationProviderBindingRepository, InMemoryOrganizationProviderBindingRepository>();
            }
        }
        else
        {
            builder.Services.RemoveAll<IOrganizationAclRepository>();
        }

        using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(
            () => app.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
        failure.Message.ShouldContain(message);
    }
}
