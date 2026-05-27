using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Forgejo;
using Hexalith.Folders.Providers.GitHub;
using Hexalith.Folders.Observability;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Queries.ProviderReadiness;

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
        services.AddOptions<FoldersTenantEventOptions>().BindConfiguration(FoldersTenantEventOptions.SectionName).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<FoldersTenantEventOptions>, FoldersTenantEventOptionsValidator>());
        services.TryAddSingleton<IUtcClock, SystemUtcClock>();
        services.TryAddSingleton<IFolderTenantAccessProjectionStore, InMemoryFolderTenantAccessProjectionStore>();
        services.TryAddSingleton(static sp => sp.GetRequiredService<IOptions<TenantAccessOptions>>().Value);
        services.TryAddSingleton(static sp => sp.GetRequiredService<IOptions<FoldersTenantEventOptions>>().Value);
        services.TryAddSingleton<TenantAccessAuthorizer>();
        services.TryAddSingleton<FolderTenantAccessHandler>();
        services.TryAddSingleton<FoldersTenantAccessEventMapper>();

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
        services.TryAddSingleton<IBranchRefPolicyReadModel, InMemoryBranchRefPolicyReadModel>();
        services.TryAddSingleton<BranchRefPolicyQueryHandler>();
        services.TryAddSingleton<IWorkspaceLockStatusReadModel, InMemoryWorkspaceLockStatusReadModel>();
        services.TryAddSingleton<WorkspaceLockStatusQueryHandler>();
        services.TryAddSingleton<IWorkspaceStatusReadModel, InMemoryWorkspaceStatusReadModel>();
        services.TryAddSingleton<WorkspaceStatusQueryHandler>();
        services.TryAddSingleton<IWorkspaceCleanupStatusReadModel, InMemoryWorkspaceCleanupStatusReadModel>();
        services.TryAddSingleton<WorkspaceCleanupStatusQueryHandler>();
        services.TryAddSingleton<ITaskStatusReadModel, InMemoryTaskStatusReadModel>();
        services.TryAddSingleton<TaskStatusQueryHandler>();

        return services;
    }

    public static IServiceCollection AddFoldersFileContextQueries(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFoldersLayeredAuthorization();
        services.TryAddSingleton<IWorkspaceFileSensitivityClassifier, WorkspaceFileSensitivityClassifier>();
        services.TryAddSingleton<IWorkspaceFileContextSource, UnavailableWorkspaceFileContextSource>();
        services.TryAddSingleton<WorkspaceFileContextQueryHandler>();

        return services;
    }

    public static IServiceCollection AddFoldersProviderReadiness(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFoldersTenantAccess();
        services.AddFoldersObservability();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IGitProvider, GitHubProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IGitProvider, ForgejoProvider>());
        services.TryAddSingleton<IProviderCapabilityAuthorizer, ProviderReadinessCapabilityAuthorizer>();
        services.TryAddSingleton<IProviderCapabilityResolver, DefaultProviderCapabilityResolver>();
        services.TryAddSingleton<IProviderCapabilityEvidenceStore, InMemoryProviderCapabilityEvidenceStore>();
        services.TryAddSingleton<ProviderCapabilityDiscoveryService>();
        services.TryAddSingleton<IProviderReadinessBindingReader, InMemoryProviderReadinessBindingReadModel>();
        services.TryAddSingleton<InMemoryProviderReadinessEvidenceStore>();
        services.TryAddSingleton<IProviderReadinessEvidenceStore>(static sp => sp.GetRequiredService<InMemoryProviderReadinessEvidenceStore>());
        services.TryAddSingleton<IProviderSupportEvidenceReadModel>(static sp => sp.GetRequiredService<InMemoryProviderReadinessEvidenceStore>());
        services.TryAddSingleton<ProviderReadinessValidationService>();
        services.TryAddSingleton<ProviderSupportEvidenceQueryHandler>();

        return services;
    }

    public static IServiceCollection AddFoldersObservability(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IFolderAuditObserver, NoOpFolderAuditObserver>());
        services.TryAddSingleton<IFolderTelemetryEmitter, FolderTelemetryEmitter>();

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
