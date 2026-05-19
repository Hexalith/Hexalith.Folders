using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders;

public static class FoldersServiceCollectionExtensions
{
    public static IServiceCollection AddFoldersTenantAccess(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<TenantAccessOptions>().BindConfiguration(TenantAccessOptions.SectionName);
        services.TryAddSingleton<IUtcClock, SystemUtcClock>();
        services.TryAddSingleton<IFolderTenantAccessProjectionStore, InMemoryFolderTenantAccessProjectionStore>();
        services.TryAddSingleton(static sp => sp.GetRequiredService<IOptions<TenantAccessOptions>>().Value);
        services.TryAddSingleton<TenantAccessAuthorizer>();
        services.TryAddSingleton<FolderTenantAccessHandler>();

        return services;
    }

    public static IServiceCollection AddFoldersEffectivePermissions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFoldersLayeredAuthorization();
        services.TryAddSingleton<EffectivePermissionsQueryHandler>();

        return services;
    }

    public static IServiceCollection AddFoldersLayeredAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFoldersTenantAccess();
        services.AddOptions<DaprPolicyEvidenceOptions>().BindConfiguration(DaprPolicyEvidenceOptions.SectionName);
        services.TryAddSingleton<IEffectivePermissionsReadModel, InMemoryEffectivePermissionsReadModel>();
        services.TryAddSingleton(static sp => sp.GetRequiredService<IOptions<DaprPolicyEvidenceOptions>>().Value);
        services.TryAddSingleton<IFolderPermissionEvidenceProvider, EffectivePermissionsFolderPermissionEvidenceProvider>();
        services.TryAddSingleton<IEventStoreAuthorizationValidator, DenyAllEventStoreAuthorizationValidator>();
        services.TryAddSingleton<IDaprPolicyEvidenceProvider, ConfigurationDaprPolicyEvidenceProvider>();
        services.TryAddSingleton<LayeredFolderAuthorizationService>();

        return services;
    }
}
