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

        foreach (IDeclaredIdempotencyIntentAdapter adapter in FoldersCatalogAdapterSource.Create())
        {
            _ = services.AddSingleton<IIdempotencyIntentAdapter>(adapter);
        }

        _ = services.AddSingleton<IHostedService, FoldersIdempotencyIntentAdapterCatalog>();
        return services;
    }
}
