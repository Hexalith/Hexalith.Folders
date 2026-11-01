using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

/// <summary>Runs the actual registered Folders gate through a host lifecycle without disabling hosted services.</summary>
public sealed class FoldersDeclaredAdmissionHostedTests
{
    /// <summary>Starts and stops a host with the actual complete registered Folders gate.</summary>
    [Fact]
    public async Task HostStartsWithEveryProductionDeclaration()
    {
        HostApplicationBuilder builder = CreateBuilder();
        using IHost host = builder.Build();
        host.Services.GetServices<IHostedService>().ShouldHaveSingleItem().ShouldBeOfType<FoldersIdempotencyIntentAdapterCatalog>();
        await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await host.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Fails the host lifecycle for each incomplete or changed registration.</summary>
    /// <param name="mismatch">The missing, duplicate, undeclared or changed policy facet.</param>
    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("undeclared")]
    [InlineData("declaration")]
    [InlineData("policy")]
    [InlineData("schema")]
    [InlineData("version")]
    [InlineData("tier")]
    [InlineData("subject")]
    [InlineData("adapter-id")]
    [InlineData("operation-id")]
    [InlineData("descriptor-version")]
    [InlineData("retention-tier")]
    public async Task HostedGateRefusesIncompleteOrDriftedProductionRegistration(string mismatch)
    {
        HostApplicationBuilder builder = CreateBuilder();
        if (mismatch == "duplicate")
        {
            builder.Services.AddSingleton<IIdempotencyIntentAdapter, CreateFolderIdempotencyIntentAdapter>();
        }
        else
        {
            ServiceDescriptor registration = builder.Services.Single(descriptor =>
                descriptor.ServiceType == typeof(IIdempotencyIntentAdapter)
                && descriptor.ImplementationType == typeof(CreateFolderIdempotencyIntentAdapter));
            builder.Services.Remove(registration);
            IDeclaredIdempotencyIntentAdapter original = new CreateFolderIdempotencyIntentAdapter();
            if (mismatch == "undeclared")
            {
                builder.Services.AddSingleton<IIdempotencyIntentAdapter>(new UndeclaredFoldersIntentAdapter(original));
            }
            else if (mismatch != "missing")
            {
                DeclaredFoldersIntentAdapter replacement = mismatch switch
                {
                    "adapter-id" => new(original, original.Declaration) { AdapterId = "changed-adapter" },
                    "operation-id" => new(original, original.Declaration) { OperationId = "changed-operation" },
                    "descriptor-version" => new(original, original.Declaration) { DescriptorVersion = 2 },
                    "retention-tier" => new(original, original.Declaration) { RetentionTier = IdempotencyReplayRetentionTier.Commit },
                    _ => new(original, FoldersDeclaredAdmissionTests.Drift(original.Declaration, mismatch)),
                };
                builder.Services.AddSingleton<IIdempotencyIntentAdapter>(replacement);
            }
        }

        using IHost host = builder.Build();
        host.Services.GetServices<IHostedService>().ShouldHaveSingleItem().ShouldBeOfType<FoldersIdempotencyIntentAdapterCatalog>();
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
        if (mismatch is not ("missing" or "duplicate"))
        {
            exception.Message.ShouldContain("Hexalith.Folders.Commands.CreateFolder");
        }
    }

    private static HostApplicationBuilder CreateBuilder()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddFoldersIdempotencyIntentAdapters();
        return builder;
    }
}
