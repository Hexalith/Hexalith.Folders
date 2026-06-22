using Hexalith.Folders.Contracts;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.ServiceDefaults;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.DomainService;
using Hexalith.Tenants.Client.Registration;
using Hexalith.Tenants.Contracts.Events;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Folders.Server;

public static class FoldersServerModule
{
    public const string ProcessRoute = "/process";

    public const string ProjectRoute = "/project";

    public const string TenantEventsRoute = FoldersTenantEventSubscription.Route;

    public const string TenantEventsPubSubName = FoldersTenantEventSubscription.PubSubName;

    public const string TenantEventsTopicName = FoldersTenantEventSubscription.TopicName;

    public const string DomainName = "folders";

    public const string CreateFolderCommandType = "Hexalith.Folders.Commands.CreateFolder";

    public const string ArchiveFolderCommandType = "Hexalith.Folders.Commands.ArchiveFolder";

    public const string CreateRepositoryBackedFolderCommandType = "Hexalith.Folders.Commands.CreateRepositoryBackedFolder";

    public const string GrantFolderAccessCommandType = "Hexalith.Folders.Commands.GrantFolderAccess";

    public const string RevokeFolderAccessCommandType = "Hexalith.Folders.Commands.RevokeFolderAccess";

    public const string BindRepositoryCommandType = "Hexalith.Folders.Commands.BindRepository";

    public const string ConfigureProviderBindingCommandType = "Hexalith.Folders.Commands.ConfigureProviderBinding";

    public const string ConfigureBranchRefPolicyCommandType = "Hexalith.Folders.Commands.ConfigureBranchRefPolicy";

    public const string PrepareWorkspaceCommandType = "Hexalith.Folders.Commands.PrepareWorkspace";

    public const string LockWorkspaceCommandType = "Hexalith.Folders.Commands.LockWorkspace";

    public const string ReleaseWorkspaceLockCommandType = "Hexalith.Folders.Commands.ReleaseWorkspaceLock";

    public const string MutateFilesCommandType = MutateWorkspaceFile.CommandTypeName;

    public const string CommitWorkspaceCommandType = CommitWorkspace.CommandTypeName;

    // Maximum length for canonical identifiers (correlation id, task id, idempotency key,
    // folder id, taskId extension). Shared between the REST endpoint regex, the processor
    // extension reader, and the rejection-event canonicalizer so a length bump cannot drift.
    public const int MaxCanonicalIdentifierLength = 128;

    public static string Description => $"{FoldersContractMetadata.ModuleName} server scaffold";

    public static IServiceCollection AddFoldersServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDaprClient();
        services.AddFoldersEffectivePermissions();
        services.AddFoldersLifecycleStatus();
        services.AddFoldersAuditQueries();
        services.AddFoldersFileContextQueries();
        services.AddFoldersProviderReadiness();
        services.AddFoldersOpsConsoleDiagnostics();

        // AddFoldersEffectivePermissions -> AddFoldersTenantAccess already binds and validate-on-start's
        // FoldersTenantEventOptions; do not rebind here.
        services.AddFoldersTenantEventProjection();
        services.AddHexalithTenants(options =>
        {
            options.PubSubName = TenantEventsPubSubName;
            options.TopicName = TenantEventsTopicName;
            options.SubscriptionRoute = TenantEventsRoute;
        });
        services.AddFoldersDomainServices();

        return services;
    }

    private static IServiceCollection AddFoldersTenantEventProjection(this IServiceCollection services)
    {
        services.TryAddSingleton<FoldersTenantEventHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventStoreDomainEventHandler<TenantCreated>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventStoreDomainEventHandler<TenantUpdated>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventStoreDomainEventHandler<TenantDisabled>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventStoreDomainEventHandler<TenantEnabled>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventStoreDomainEventHandler<UserAddedToTenant>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventStoreDomainEventHandler<UserRemovedFromTenant>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventStoreDomainEventHandler<UserRoleChanged>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventStoreDomainEventHandler<TenantConfigurationSet>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventStoreDomainEventHandler<TenantConfigurationRemoved>, FoldersTenantEventHandler>());
        return services;
    }

    public static IEndpointRouteBuilder MapFoldersServerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapDefaultEndpoints();
        endpoints.MapGet("/", () => Description);
        endpoints.MapFoldersDomainServiceEndpoints();
        endpoints.MapProviderReadinessEndpoints();
        endpoints.MapOpsConsoleDiagnosticsEndpoints();
        endpoints.MapAuditEndpoints();
        endpoints.MapEventStoreDomainEvents();

        return endpoints;
    }
}
