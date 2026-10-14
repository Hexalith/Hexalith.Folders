using Dapr;
using Hexalith.Folders;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Workers.RepositoryProvisioning;
using Hexalith.Folders.Workers.SemanticIndexing;
using Hexalith.Folders.Workers.Tenants.TenantEventHandlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Tenants.Client.Registration;
using Hexalith.Tenants.Contracts.Events;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Workers;

public static partial class FoldersWorkersModule
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
        services.AddAuthentication().AddEventStoreSidecarChannelScheme();
        services.AddAuthorization(options => options.AddPolicy(
            EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy,
            policy => policy
                .AddAuthenticationSchemes(EventStoreWorkloadAuthenticationDefaults.SidecarChannelScheme)
                .RequireAuthenticatedUser()
                .RequireClaim(EventStoreWorkloadAuthenticationDefaults.ChannelClaimType)));
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
        services.AddOptions<EventStoreDomainEventsOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                options.PubSubName = SubscriptionPubSubName(configuration))
            .ValidateOnStart();
        services.AddFoldersTenantEventProjection();
        services.AddFoldersRepositoryProvisioningWorkers();

        return services;
    }

    public static IServiceCollection AddFoldersSemanticIndexingWorkers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDaprClient();
        services.AddFoldersSemanticIndexingBridge();

        // The fail-closed policy evaluator gates indexing on tenant-access and folder ACL/action freshness, so the
        // authorization projections must be resolvable even when this registration is used standalone.
        services.AddFoldersLayeredAuthorization();
        services.AddEventStoreReadModelStore();
        services.AddHttpClient<IOrganizationStreamSequenceReader, DaprOrganizationStreamSequenceReader>();
        services.RemoveAll<IEffectivePermissionsReadModel>();
        services.TryAddSingleton<EventStoreOrganizationEffectivePermissionsReadModel>();
        services.TryAddSingleton<EventStoreFolderEffectivePermissionsReadModel>();
        services.TryAddSingleton<IEffectivePermissionsReadModel>(static sp =>
            sp.GetRequiredService<EventStoreFolderEffectivePermissionsReadModel>());
        services.TryAddSingleton<IFolderAclReadModel>(static sp =>
            sp.GetRequiredService<EventStoreFolderEffectivePermissionsReadModel>());
        services.TryAddTransient<FoldersOrganizationAclEventProcessor>();
        services.TryAddTransient<FoldersFolderPermissionEventProcessor>();
        services.RemoveAll<ISemanticIndexingBridgeReadModel>();
        services.RemoveAll<ISemanticIndexingBridgeWriter>();
        services.TryAddSingleton<EventStoreSemanticIndexingBridgeStore>();
        services.TryAddSingleton<ISemanticIndexingBridgeReadModel>(static sp => sp.GetRequiredService<EventStoreSemanticIndexingBridgeStore>());
        services.TryAddSingleton<ISemanticIndexingBridgeWriter>(static sp => sp.GetRequiredService<EventStoreSemanticIndexingBridgeStore>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ISemanticIndexingPolicyEvaluator, FailClosedSemanticIndexingPolicyEvaluator>();
        services.TryAddSingleton<ISemanticIndexingContentMaterializer, MetadataDerivedSemanticIndexingContentMaterializer>();
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
        endpoints.MapFoldersTenantEvents();
        endpoints.MapFoldersSemanticIndexingEvents();
        endpoints.MapFolderPermissionMaintenance();

        return endpoints;
    }

    private static IEndpointRouteBuilder MapFoldersTenantEvents(this IEndpointRouteBuilder endpoints)
    {
        EventStoreDomainEventsOptions options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<EventStoreDomainEventsOptions>>().Value;

        _ = endpoints.MapPost(options.SubscriptionRoute, async (
            EventStoreDomainEventEnvelope envelope,
            EventStoreDomainEventProcessor processor,
            CancellationToken cancellationToken) =>
        {
            EventStoreDomainEventProcessingResult result = await processor
                .ProcessAsync(envelope, cancellationToken)
                .ConfigureAwait(false);

            return result switch
            {
                EventStoreDomainEventProcessingResult.Processed => Results.Ok(),
                EventStoreDomainEventProcessingResult.Duplicate => Results.Ok(),
                EventStoreDomainEventProcessingResult.SkippedUnknownEventType => Results.Ok(),
                EventStoreDomainEventProcessingResult.SkippedNoHandlers => Results.Ok(),
                EventStoreDomainEventProcessingResult.SkippedAggregateMismatch => Results.Ok(),

                // Tenant-access projection payload failures must be retryable. The processor releases
                // the message claim for this result, and the response body stays metadata-only.
                EventStoreDomainEventProcessingResult.FailedInvalidPayload => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
                _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
            };
        }).WithTopic(new TopicOptions
        {
            PubsubName = options.PubSubName,
            Name = options.TopicName,
            DeadLetterTopic = "deadletter.tenants.events",
        })
            .RequireAuthorization(EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy);

        return endpoints;
    }

    private static IEndpointRouteBuilder MapFoldersSemanticIndexingEvents(this IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapPost(FoldersSemanticIndexingDefaults.DomainEventsRoute, async (
            Hexalith.EventStore.Client.Subscriptions.EventStoreDomainEventEnvelope envelope,
            FoldersSemanticIndexingEventProcessor processor,
            FoldersOrganizationAclEventProcessor organizationProcessor,
            FoldersFolderPermissionEventProcessor folderPermissionProcessor,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await organizationProcessor.ProcessAsync(envelope, cancellationToken).ConfigureAwait(false);
                await folderPermissionProcessor.ProcessAsync(envelope, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception failure)
            {
                PermissionProjectionFailure(loggerFactory.CreateLogger(typeof(FoldersWorkersModule)),
                    "permission_projection_failed", "permission_projection",
                    failure.GetType().FullName ?? failure.GetType().Name);
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
            }

            FoldersSemanticIndexingEventProcessingResult result = await processor
                .ProcessAsync(envelope, cancellationToken)
                .ConfigureAwait(false);
            return result switch
            {
                FoldersSemanticIndexingEventProcessingResult.Processed => Results.Ok(),
                FoldersSemanticIndexingEventProcessingResult.SkippedUnknownEventType => Results.Ok(),

                // Permission projection rejects malformed retained folder payloads above with a retryable
                // 500. Only a semantic-only payload failure that passed that gate is acknowledged here.
                FoldersSemanticIndexingEventProcessingResult.FailedInvalidPayload => Results.Ok(),
                _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
            };
        }).WithTopic(new TopicOptions
        {
            PubsubName = SubscriptionPubSubName(endpoints.ServiceProvider.GetRequiredService<IConfiguration>()),
            Name = FoldersSemanticIndexingDefaults.DomainEventsTopicName,
            DeadLetterTopic = "deadletter.folders.events",
        })
            .RequireAuthorization(EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy);

        return endpoints;
    }

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Warning,
        Message = "Folder permission projection failed category={Category} stage={Stage} failure_type={FailureType}")]
    private static partial void PermissionProjectionFailure(ILogger logger, string category, string stage, string failureType);

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

    private static string SubscriptionPubSubName(IConfiguration configuration)
        => configuration["Folders:Workers:PubSubName"] ?? TenantEventsPubSubName;

    private sealed class FoldersTenantEventSubscriptionOptionsValidator(IConfiguration configuration)
        : IValidateOptions<EventStoreDomainEventsOptions>
    {
        public ValidateOptionsResult Validate(string? name, EventStoreDomainEventsOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (!string.Equals(options.PubSubName, SubscriptionPubSubName(configuration), StringComparison.Ordinal))
            {
                return ValidateOptionsResult.Fail($"Tenants PubSubName must match the configured Workers subscription component for {Name}.");
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
