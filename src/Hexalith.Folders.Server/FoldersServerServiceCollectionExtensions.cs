using Hexalith.EventStore.Client.Registration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Folders.Server;

public static class FoldersServerServiceCollectionExtensions
{
    public static IServiceCollection AddFoldersDomainServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddEventStore(typeof(FoldersModule).Assembly);
        services.TryAddScoped<FoldersDomainServiceRequestHandler>();

        return services;
    }
}
