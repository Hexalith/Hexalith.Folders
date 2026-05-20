using Hexalith.Folders.Contracts;
using Hexalith.Folders.ServiceDefaults;
using Hexalith.Tenants.Client.Registration;
using Hexalith.Tenants.Client.Handlers;
using Hexalith.Tenants.Client.Subscription;
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

    public const string TenantEventsRoute = "/tenants/events";

    public const string TenantEventsPubSubName = "pubsub";

    public const string TenantEventsTopicName = "system.tenants.events";

    public const string DomainName = "folders";

    public const string ArchiveFolderCommandType = "Hexalith.Folders.Commands.ArchiveFolder";

    public static string Description => $"{FoldersContractMetadata.ModuleName} server scaffold";

    public static IServiceCollection AddFoldersServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDaprClient();
        services.AddFoldersEffectivePermissions();
        services.AddFoldersLifecycleStatus();
        services.AddFoldersTenantEventProjection();
        services.AddHexalithTenants(options =>
        {
            options.PubSubName = TenantEventsPubSubName;
            options.TopicName = TenantEventsTopicName;
        });
        services.AddFoldersDomainServices();

        return services;
    }

    private static IServiceCollection AddFoldersTenantEventProjection(this IServiceCollection services)
    {
        services.TryAddSingleton<FoldersTenantEventHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantEventHandler<TenantCreated>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantEventHandler<TenantUpdated>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantEventHandler<TenantDisabled>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantEventHandler<TenantEnabled>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantEventHandler<UserAddedToTenant>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantEventHandler<UserRemovedFromTenant>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantEventHandler<UserRoleChanged>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantEventHandler<TenantConfigurationSet>, FoldersTenantEventHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantEventHandler<TenantConfigurationRemoved>, FoldersTenantEventHandler>());
        return services;
    }

    public static IEndpointRouteBuilder MapFoldersServerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapDefaultEndpoints();
        endpoints.MapGet("/", () => Description);
        endpoints.MapFoldersDomainServiceEndpoints();
        endpoints.MapTenantEventSubscription();

        return endpoints;
    }
}
