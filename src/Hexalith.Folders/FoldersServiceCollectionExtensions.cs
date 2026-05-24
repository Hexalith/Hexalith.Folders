using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;

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

    // Opt-in in-memory IFolderRepository registration. Production AppHosts MUST register
    // a real EventStore-backed repository instead; integration tests and dev hosts call
    // this method explicitly so a production composition that forgets to register a
    // repository fails loud at startup rather than silently running on a dictionary.
    // Also ensures TimeProvider.System is available — the in-memory repository needs one
    // for projection observed-at timestamps and the constructor would otherwise resolve a
    // different default depending on whether the host registered one.
    public static IServiceCollection AddInMemoryFolderRepository(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IFolderRepository, InMemoryFolderRepository>();

        return services;
    }

    public static IServiceCollection AddFoldersEffectivePermissions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFoldersLayeredAuthorization();
        services.TryAddSingleton<EffectivePermissionsQueryHandler>();

        return services;
    }

    public static IServiceCollection AddFoldersLifecycleStatus(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFoldersLayeredAuthorization();
        services.TryAddSingleton<IFolderLifecycleStatusReadModel, InMemoryFolderLifecycleStatusReadModel>();
        services.TryAddSingleton<FolderLifecycleStatusQueryHandler>();

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
