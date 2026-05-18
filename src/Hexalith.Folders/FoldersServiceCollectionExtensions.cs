using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Folders;

public static class FoldersServiceCollectionExtensions
{
    public static IServiceCollection AddFoldersTenantAccess(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IUtcClock, SystemUtcClock>();
        services.TryAddSingleton<IFolderTenantAccessProjectionStore, InMemoryFolderTenantAccessProjectionStore>();
        services.TryAddSingleton(new TenantAccessOptions());
        services.TryAddSingleton<TenantAccessAuthorizer>();
        services.TryAddSingleton<FolderTenantAccessHandler>();

        return services;
    }
}
