using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Server;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

public sealed class FoldersIdempotencyIntentAdapterCatalogTests
{
    [Fact]
    public void RegisteredAdaptersShouldMatchEveryFoldersMutationCommandType()
    {
        using ServiceProvider provider = BuildFoldersAdapters();
        IIdempotencyIntentAdapter[] adapters = [.. provider.GetServices<IIdempotencyIntentAdapter>()];

        adapters.Select(static adapter => adapter.CommandType)
            .Order(StringComparer.Ordinal)
            .ShouldBe(ExpectedCommandTypes.Order(StringComparer.Ordinal));
        adapters.ShouldAllBe(static adapter => adapter.AdapterId == FoldersCanonicalIntentBuilder.AdapterId);
        adapters.Single(static adapter => adapter.CommandType == FoldersServerModule.CommitWorkspaceCommandType)
            .RetentionTier.ShouldBe(IdempotencyReplayRetentionTier.Commit);
        adapters.Where(static adapter => adapter.CommandType != FoldersServerModule.CommitWorkspaceCommandType)
            .ShouldAllBe(static adapter => adapter.RetentionTier == IdempotencyReplayRetentionTier.Mutation);
    }

    [Fact]
    public async Task CatalogShouldStartWhenTheRegisteredSetIsComplete()
    {
        using ServiceProvider provider = BuildFoldersAdapters();
        FoldersIdempotencyIntentAdapterCatalog catalog = ActivatorUtilities.CreateInstance<FoldersIdempotencyIntentAdapterCatalog>(provider);

        await catalog.StartAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CatalogShouldFailClosedWhenAMutationCommandTypeIsMissing()
    {
        ServiceCollection services = new();
        _ = services.AddIdempotencyIntentAdapter<CreateFolderIdempotencyIntentAdapter>();
        using ServiceProvider provider = services.BuildServiceProvider();
        FoldersIdempotencyIntentAdapterCatalog catalog = ActivatorUtilities.CreateInstance<FoldersIdempotencyIntentAdapterCatalog>(provider);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => catalog.StartAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("missing or unexpected");
    }

    [Fact]
    public async Task CatalogShouldFailClosedWhenACommandTypeIsDuplicated()
    {
        ServiceCollection services = new();
        _ = services.AddFoldersIdempotencyIntentAdapters();
        _ = services.AddSingleton<IIdempotencyIntentAdapter, CreateFolderIdempotencyIntentAdapter>();
        using ServiceProvider provider = services.BuildServiceProvider();
        FoldersIdempotencyIntentAdapterCatalog catalog = ActivatorUtilities.CreateInstance<FoldersIdempotencyIntentAdapterCatalog>(provider);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => catalog.StartAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("Multiple trusted idempotency adapters");
    }

    private static ServiceProvider BuildFoldersAdapters()
    {
        ServiceCollection services = new();
        _ = services.AddFoldersIdempotencyIntentAdapters();
        return services.BuildServiceProvider();
    }

    private static readonly string[] ExpectedCommandTypes =
    [
        FoldersServerModule.CreateFolderCommandType,
        FoldersServerModule.ArchiveFolderCommandType,
        FoldersServerModule.GrantFolderAccessCommandType,
        FoldersServerModule.RevokeFolderAccessCommandType,
        FoldersServerModule.ConfigureProviderBindingCommandType,
        FoldersServerModule.CreateRepositoryBackedFolderCommandType,
        FoldersServerModule.BindRepositoryCommandType,
        FoldersServerModule.ConfigureBranchRefPolicyCommandType,
        FoldersServerModule.PrepareWorkspaceCommandType,
        FoldersServerModule.LockWorkspaceCommandType,
        FoldersServerModule.ReleaseWorkspaceLockCommandType,
        FoldersServerModule.MutateFilesCommandType,
        FoldersServerModule.CommitWorkspaceCommandType,
    ];
}
