using Hexalith.Folders;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Contracts;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Workers.RepositoryProvisioning;
using Hexalith.Folders.Workers.SemanticIndexing;
using Hexalith.Folders.Workers.Tenants.TenantEventHandlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.DomainService;
using Hexalith.Tenants.Client.Registration;
using Hexalith.Tenants.Contracts.Events;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Workers;

public static class FoldersWorkersModule
{
    public const string AppId = FoldersTenantEventSubscription.AppId;

    public const string TenantEventsRoute = FoldersTenantEventSubscription.Route;

    public const string TenantEventsPubSubName = FoldersTenantEventSubscription.PubSubName;

    public const string TenantEventsTopicName = FoldersTenantEventSubscription.TopicName;

    public static string Name => $"{FoldersContractMetadata.ModuleName}.Workers";

    public static string Description => $"{Name} tenant-event worker";

    public static IServiceCollection AddFoldersTenantEventWorkers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDaprClient();
        services.AddFoldersSemanticIndexingWorkers();

        // AddFoldersTenantAccess already binds and validate-on-start's FoldersTenantEventOptions; do not rebind here.
        services.AddFoldersTenantAccess();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EventStoreDomainEventsOptions>, FoldersTenantEventSubscriptionOptionsValidator>());
        services.AddHexalithTenants(options =>
        {
            options.PubSubName = TenantEventsPubSubName;
            options.TopicName = TenantEventsTopicName;
            options.SubscriptionRoute = TenantEventsRoute;
        });
        services.AddOptions<EventStoreDomainEventsOptions>().ValidateOnStart();
        services.AddFoldersTenantEventProjection();
        services.AddFoldersRepositoryProvisioningWorkers();

        return services;
    }

    public static IServiceCollection AddFoldersSemanticIndexingWorkers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDaprClient();
        services.AddFoldersSemanticIndexingBridge();

        // The fail-closed policy evaluator gates indexing on tenant-access authority, so the tenant-access
        // projection store must be resolvable even when this registration is used standalone.
        services.AddFoldersTenantAccess();
        services.AddEventStoreReadModelStore();
        services.RemoveAll<ISemanticIndexingBridgeReadModel>();
        services.RemoveAll<ISemanticIndexingBridgeWriter>();
        services.TryAddSingleton<EventStoreSemanticIndexingBridgeStore>();
        services.TryAddSingleton<ISemanticIndexingBridgeReadModel>(static sp => sp.GetRequiredService<EventStoreSemanticIndexingBridgeStore>());
        services.TryAddSingleton<ISemanticIndexingBridgeWriter>(static sp => sp.GetRequiredService<EventStoreSemanticIndexingBridgeStore>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ISemanticIndexingPolicyEvaluator, FailClosedSemanticIndexingPolicyEvaluator>();
        services.TryAddSingleton<ISemanticIndexingContentMaterializer, FailClosedSemanticIndexingContentMaterializer>();
        services.TryAddTransient<ISemanticIndexingPort, MemoriesSemanticIndexingPort>();
        services.TryAddTransient<SemanticIndexingProcessManager>();
        services.TryAddTransient<FoldersSemanticIndexingEventProcessor>();

        return services;
    }

    public static IServiceCollection AddFoldersRepositoryProvisioningWorkers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFoldersProviderReadiness();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(static sp => new RepositoryProvisioningProcessManager(
            sp.GetRequiredService<IFolderRepository>(),
            sp.GetRequiredService<IProviderCapabilityResolver>(),
            sp.GetService<TimeProvider>()));

        return services;
    }

    public static IEndpointRouteBuilder MapFoldersTenantEventWorkerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/", () => Description);
        endpoints.MapEventStoreDomainEvents();
        endpoints.MapFoldersSemanticIndexingEvents();

        return endpoints;
    }

    private static IEndpointRouteBuilder MapFoldersSemanticIndexingEvents(this IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapPost(FoldersSemanticIndexingDefaults.DomainEventsRoute, async (
            Hexalith.EventStore.Client.Subscriptions.EventStoreDomainEventEnvelope envelope,
            FoldersSemanticIndexingEventProcessor processor,
            CancellationToken cancellationToken) =>
        {
            FoldersSemanticIndexingEventProcessingResult result = await processor
                .ProcessAsync(envelope, cancellationToken)
                .ConfigureAwait(false);
            return result switch
            {
                FoldersSemanticIndexingEventProcessingResult.Processed => Results.Ok(),
                FoldersSemanticIndexingEventProcessingResult.SkippedUnknownEventType => Results.Ok(),

                // A payload that cannot be deserialized or is not an IFolderEvent is a deterministic, permanent
                // failure of this message. Return a success status so Dapr drops it instead of redelivering the same
                // poison message forever; the drop is recorded as a metadata-only warning by the processor. Genuinely
                // transient failures still surface as unhandled exceptions -> 500 -> Dapr retry / dead-letter routing.
                FoldersSemanticIndexingEventProcessingResult.FailedInvalidPayload => Results.Ok(),
                _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
            };
        }).WithTopic(
            FoldersSemanticIndexingDefaults.PubSubName,
            FoldersSemanticIndexingDefaults.DomainEventsTopicName);

        return endpoints;
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

    private sealed class FoldersTenantEventSubscriptionOptionsValidator : IValidateOptions<EventStoreDomainEventsOptions>
    {
        public ValidateOptionsResult Validate(string? name, EventStoreDomainEventsOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (!string.Equals(options.PubSubName, TenantEventsPubSubName, StringComparison.Ordinal))
            {
                return ValidateOptionsResult.Fail($"Tenants PubSubName must be '{TenantEventsPubSubName}' for {Name}.");
            }

            if (!string.Equals(options.TopicName, TenantEventsTopicName, StringComparison.Ordinal))
            {
                return ValidateOptionsResult.Fail($"Tenants TopicName must be '{TenantEventsTopicName}' for {Name}.");
            }

            if (!string.Equals(options.SubscriptionRoute, TenantEventsRoute, StringComparison.Ordinal))
            {
                return ValidateOptionsResult.Fail($"Tenants SubscriptionRoute must be '{TenantEventsRoute}' for {Name}.");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
