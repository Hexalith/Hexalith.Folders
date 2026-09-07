using Hexalith.EventStore.DomainService;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.EventStore;

/// <summary>Registers Folders trusted idempotency adapters on the EventStore command host.</summary>
public static class FoldersIdempotencyIntentAdapterServiceCollectionExtensions
{
    /// <summary>Adds one trusted adapter per Folders mutation command type.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFoldersIdempotencyIntentAdapters(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddIdempotencyIntentAdapter<CreateFolderIdempotencyIntentAdapter>();
        _ = services.AddIdempotencyIntentAdapter<ArchiveFolderIdempotencyIntentAdapter>();
        _ = services.AddIdempotencyIntentAdapter<GrantFolderAccessIdempotencyIntentAdapter>();
        _ = services.AddIdempotencyIntentAdapter<RevokeFolderAccessIdempotencyIntentAdapter>();
        _ = services.AddIdempotencyIntentAdapter<ConfigureProviderBindingIdempotencyIntentAdapter>();
        _ = services.AddIdempotencyIntentAdapter<CreateRepositoryBackedFolderIdempotencyIntentAdapter>();
        _ = services.AddIdempotencyIntentAdapter<BindRepositoryIdempotencyIntentAdapter>();
        _ = services.AddIdempotencyIntentAdapter<ConfigureBranchRefPolicyIdempotencyIntentAdapter>();
        _ = services.AddIdempotencyIntentAdapter<PrepareWorkspaceIdempotencyIntentAdapter>();
        _ = services.AddIdempotencyIntentAdapter<LockWorkspaceIdempotencyIntentAdapter>();
        _ = services.AddIdempotencyIntentAdapter<ReleaseWorkspaceLockIdempotencyIntentAdapter>();
        _ = services.AddIdempotencyIntentAdapter<MutateFilesIdempotencyIntentAdapter>();
        _ = services.AddIdempotencyIntentAdapter<CommitWorkspaceIdempotencyIntentAdapter>();
        _ = services.AddSingleton<IHostedService, FoldersIdempotencyIntentAdapterCatalog>();
        return services;
    }
}
