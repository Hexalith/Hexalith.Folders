using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Forgejo;
using Hexalith.Folders.Providers.GitHub;
using Hexalith.Folders.Observability;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Credentials;
using Hexalith.Folders.Queries.Audit;
using Hexalith.Folders.Queries.ContextSearch;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Queries.FolderAccess;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Queries.OpsConsole;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Projections.SemanticIndexing;

using Dapr.Client;

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
        services.TryAddSingleton<IWorkspaceTransitionEvidenceReadModel, InMemoryWorkspaceTransitionEvidenceReadModel>();
        services.TryAddSingleton<WorkspaceTransitionEvidenceQueryHandler>();
        services.TryAddSingleton<IWorkspaceCleanupStatusReadModel, InMemoryWorkspaceCleanupStatusReadModel>();
        services.TryAddSingleton<WorkspaceCleanupStatusQueryHandler>();
        services.TryAddSingleton<ITaskStatusReadModel, InMemoryTaskStatusReadModel>();
        services.TryAddSingleton<TaskStatusQueryHandler>();
        services.TryAddSingleton<ListFolderAclEntriesQueryHandler>();

        return services;
    }

    public static IServiceCollection AddFoldersOpsConsoleDiagnostics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Layered authorization brings TenantAccessAuthorizer (tenant-scoped diagnostics) and
        // LayeredFolderAuthorizationService (folder/workspace-scoped diagnostics) plus IUtcClock.
        services.AddFoldersLayeredAuthorization();
        services.TryAddSingleton<IOpsConsoleDiagnosticsReadModel, InMemoryOpsConsoleDiagnosticsReadModel>();
        services.TryAddSingleton<TenantScopedDiagnosticsQueryHandler>();
        services.TryAddSingleton<FolderScopedDiagnosticsQueryHandler>();

        return services;
    }

    public static IServiceCollection AddFoldersAuditQueries(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFoldersLayeredAuthorization();
        services.TryAddSingleton<InMemoryAuditTrailReadModel>();
        services.TryAddSingleton<IAuditTrailReadModel>(static sp => sp.GetRequiredService<InMemoryAuditTrailReadModel>());
        services.TryAddSingleton<AuditTrailQueryHandler>();
        services.TryAddSingleton<InMemoryAuditRecordReadModel>();
        services.TryAddSingleton<IAuditRecordReadModel>(static sp => sp.GetRequiredService<InMemoryAuditRecordReadModel>());
        services.TryAddSingleton<AuditRecordQueryHandler>();
        services.TryAddSingleton<InMemoryOperationTimelineReadModel>();
        services.TryAddSingleton<IOperationTimelineReadModel>(static sp => sp.GetRequiredService<InMemoryOperationTimelineReadModel>());
        services.TryAddSingleton<OperationTimelineQueryHandler>();
        services.TryAddSingleton<InMemoryOperationTimelineEntryReadModel>();
        services.TryAddSingleton<IOperationTimelineEntryReadModel>(static sp => sp.GetRequiredService<InMemoryOperationTimelineEntryReadModel>());
        services.TryAddSingleton<OperationTimelineEntryQueryHandler>();

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

    /// <summary>
    /// Registers the Story 10.5 authorized context-search facade over the Memories search index. The egress source
    /// (<see cref="IFolderSearchSource"/>) and the bridge read model default to fail-safe <c>Unavailable</c>
    /// implementations; a host with a live Memories gateway (the Server) overrides <see cref="IFolderSearchSource"/>.
    /// Registered scoped so a live gateway backed by a typed HttpClient does not become a captive dependency.
    /// </summary>
    public static IServiceCollection AddFoldersContextSearchQueries(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFoldersLayeredAuthorization();
        services.TryAddScoped<IFolderSearchSource, UnavailableFolderSearchSource>();
        services.TryAddScoped<ISemanticIndexingBridgeReadModel, UnavailableSemanticIndexingBridgeReadModel>();
        services.TryAddScoped<ContextSearchQueryHandler>();
        services.TryAddScoped<FolderIndexingStatusQueryHandler>();

        return services;
    }

    public static IServiceCollection AddFoldersSemanticIndexingBridge(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<InMemorySemanticIndexingBridgeStore>();
        services.TryAddSingleton<ISemanticIndexingBridgeReadModel>(static sp => sp.GetRequiredService<InMemorySemanticIndexingBridgeStore>());
        services.TryAddSingleton<ISemanticIndexingBridgeWriter>(static sp => sp.GetRequiredService<InMemorySemanticIndexingBridgeStore>());

        return services;
    }

    public static IServiceCollection AddFoldersProviderReadiness(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFoldersTenantAccess();
        services.AddFoldersObservability();
        services.TryAddSingleton<IGitHubCredentialResolver, UnconfiguredGitHubCredentialResolver>();
        services.TryAddSingleton<IForgejoCredentialResolver, UnconfiguredForgejoCredentialResolver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IGitProvider, GitHubProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IGitProvider, ForgejoProvider>());
        services.TryAddSingleton<IProviderCapabilityAuthorizer, ProviderReadinessCapabilityAuthorizer>();
        services.TryAddSingleton<IProviderRepositoryTargetResolver, UnconfiguredProviderRepositoryTargetResolver>();
        services.TryAddSingleton<IProviderCapabilityResolver, DefaultProviderCapabilityResolver>();
        services.TryAddSingleton<IProviderCapabilityEvidenceStore, InMemoryProviderCapabilityEvidenceStore>();
        services.TryAddSingleton<ProviderCapabilityDiscoveryService>();
        services.TryAddSingleton<IProviderReadinessBindingReader, InMemoryProviderReadinessBindingReadModel>();
        services.TryAddSingleton<InMemoryProviderReadinessEvidenceStore>();
        services.TryAddSingleton<IProviderReadinessEvidenceStore>(static sp => sp.GetRequiredService<InMemoryProviderReadinessEvidenceStore>());
        services.TryAddSingleton<IProviderSupportEvidenceReadModel>(static sp => sp.GetRequiredService<InMemoryProviderReadinessEvidenceStore>());
        services.TryAddSingleton<ProviderReadinessValidationService>();
        services.TryAddSingleton<ProviderSupportEvidenceQueryHandler>();
        services.TryAddSingleton<GetProviderBindingQueryHandler>();

        return services;
    }

    public static IServiceCollection AddFoldersDaprProviderCredentialResolution(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFoldersProviderReadiness();
        services.TryAddSingleton(static _ => new DaprClientBuilder().Build());
        services.AddOptions<FoldersProviderCredentialOptions>()
            .BindConfiguration(FoldersProviderCredentialOptions.SectionName)
            .Validate(static options => !string.IsNullOrWhiteSpace(options.SecretStoreName), "Provider credential secret store name is required.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.AccessTokenKey), "Provider credential access-token key is required.")
            .ValidateOnStart();

        services.RemoveAll<IProviderCredentialReferenceResolver>();
        services.RemoveAll<IProviderCredentialSecretStoreClient>();
        services.RemoveAll<IGitHubCredentialResolver>();
        services.RemoveAll<IForgejoCredentialResolver>();
        services.RemoveAll<IGitProvider>();

        services.TryAddSingleton<IProviderCredentialSecretStoreClient, DaprProviderCredentialSecretStoreClient>();
        services.TryAddSingleton<IProviderCredentialReferenceResolver, DaprProviderCredentialReferenceResolver>();
        services.TryAddSingleton<IGitHubCredentialResolver, DaprBackedGitHubCredentialResolver>();
        services.TryAddSingleton<IForgejoCredentialResolver, DaprBackedForgejoCredentialResolver>();
        services.AddSingleton<IGitProvider>(static sp => new GitHubProvider(
            sp.GetRequiredService<IGitHubCredentialResolver>(),
            new OctokitGitHubApiClientFactory(),
            sp.GetRequiredService<IProviderRepositoryTargetResolver>()));
        services.AddSingleton<IGitProvider>(static sp => new ForgejoProvider(
            sp.GetRequiredService<IForgejoCredentialResolver>(),
            new ForgejoHttpApiClientFactory()));

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
