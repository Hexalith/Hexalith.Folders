using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.ServiceDefaults;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Client.Registration;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Observability;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Folders.Server;

/// <summary>Composes the Folders server host: its services and its request pipeline.</summary>
public static class FoldersServerHostComposition
{
    /// <summary>Registers the Folders server host services and validates the API routing mode.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The validated API routing mode.</returns>
    /// <exception cref="InvalidOperationException">The API routing mode setting is empty or unknown.</exception>
    public static FoldersApiRoutingMode AddFoldersServerHost(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Validate before any registration so an invalid mode fails host startup.
        FoldersApiRoutingMode mode = FoldersApiRouting.ResolveMode(builder.Configuration);

        builder.Services.AddFoldersServer();
        builder.AddEventStoreDomainService(typeof(FoldersModule).Assembly);
        // The SDK owns exporters, HTTP instrumentation and Dapr-partitioned resilience.
        // Enroll only Folders' module signals and monitored readiness into those defaults.
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(FolderTelemetryNames.ServiceName))
            .WithMetrics(metrics => metrics.AddMeter(FolderTelemetryNames.MeterName))
            .WithTracing(tracing => tracing.AddSource(FolderTelemetryNames.ActivitySourceName));
        builder.Services.TryAddSingleton<IReadinessSnapshotSource, HealthyReadinessSnapshotSource>();
        builder.Services.AddHealthChecks()
            .AddCheck<MonitoredSnapshotReadinessCheck>("monitored-snapshots", tags: [Hexalith.Folders.ServiceDefaults.Extensions.ReadinessTag]);
        builder.Services.AddFoldersOperationalStatusPersistence();
        builder.Services.AddOptions<FoldersProviderBindingOptions>().BindConfiguration(FoldersProviderBindingOptions.SectionName);
        builder.Services.AddFoldersDaprProviderCredentialResolution();
        builder.Services.AddFoldersProviderBindingPersistence();
        builder.Services.AddTransient<FoldersGatewayHumanBearerHandler>();
        builder.Services.AddEventStoreGatewayClient()
            .ConfigureHttpClient(client => client.BaseAddress = new Uri(
                builder.Configuration["EventStore:Gateway:BaseAddress"] ?? "http://eventstore"))
            .AddHttpMessageHandler<FoldersGatewayHumanBearerHandler>();
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore, EventStoreFolderTenantAccessProjectionStore>();
        builder.Services.RemoveAll<IEffectivePermissionsReadModel>();
        builder.Services.AddSingleton<EventStoreOrganizationEffectivePermissionsReadModel>();
        builder.Services.AddSingleton<EventStoreFolderEffectivePermissionsReadModel>();
        builder.Services.AddSingleton<IEffectivePermissionsReadModel>(static sp =>
            sp.GetRequiredService<EventStoreFolderEffectivePermissionsReadModel>());
        builder.Services.AddSingleton<IFolderAclReadModel>(static sp =>
            sp.GetRequiredService<EventStoreFolderEffectivePermissionsReadModel>());
        builder.Services.AddFoldersProductionAuthentication(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler,
            FoldersHumanAuthorizationResultHandler>();
        builder.Services.AddSingleton<HumanAuthorityReceiptVerifier>();
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, VerifiedReceiptEventStoreAuthorizationValidator>();

        // This host is the EventStore domain-service target in every environment. It must
        // fold actor-supplied state and return events; an in-memory writer would acknowledge
        // commands without creating a durable EventStore stream during local development.
        builder.Services.RemoveAll<IFolderRepository>();
        builder.Services.RemoveAll<IOrganizationProviderBindingRepository>();
        builder.Services.RemoveAll<IOrganizationAclRepository>();
        builder.Services.AddScoped<IFolderRepository, ActorFoldedFolderRepository>();
        builder.Services.AddScoped<ActorFoldedOrganizationProviderBindingRepository>();
        builder.Services.AddScoped<IOrganizationProviderBindingRepository>(static sp =>
            sp.GetRequiredService<ActorFoldedOrganizationProviderBindingRepository>());
        builder.Services.AddScoped<IOrganizationAclRepository>(static sp =>
            sp.GetRequiredService<ActorFoldedOrganizationProviderBindingRepository>());

        builder.Services.AddSingleton(new LegacyProjectionCatalogRegistration(
            FoldersRuntimeCatalogDeclarations.Routes.Where(route => route.Kind == "projection").ToArray()));
        builder.Services.AddEventStoreDevelopmentDomainCatalog(builder.Configuration,
            typeof(FoldersRuntimeCatalogDeclarations).Assembly);
        builder.Services.AddHostedService<FolderRepositoryStartupAssertion>();

        // Validate scoped service lifetimes at build time so a missing or misconfigured DI
        // registration (e.g., a forgotten ILayeredFolderAuthorizationResultAccessor or a captive
        // dependency) fails at host startup rather than on the first request.
        builder.Host.UseDefaultServiceProvider(static options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

        return mode;
    }

    /// <summary>Composes the Folders server request pipeline for the configured API routing mode.</summary>
    /// <param name="app">The built web application.</param>
    /// <returns>The same application.</returns>
    /// <exception cref="InvalidOperationException">The API routing mode setting is empty or unknown.</exception>
    public static WebApplication UseFoldersServerPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        FoldersApiRoutingMode mode = FoldersApiRouting.ResolveMode(app.Configuration);

        app.UseCloudEvents();
        app.UseAuthentication();
        app.UseFoldersApiRouteTelemetry();
        if (mode != FoldersApiRoutingMode.V1Only)
        {
            // The seam rewrites /api/v2 requests onto their v1 handlers, so it must run before routing
            // selects an endpoint. V1Only keeps the implicit routing position unchanged.
            app.UsePd10V2CandidateCompatibilitySeam();
            app.UseRouting();
            if (mode == FoldersApiRoutingMode.V2Only)
            {
                app.UsePd10HistoricalRouteRetirement();
            }
        }

        app.UseEventStoreHttpHostSecurity();
        app.UseAuthorization();
        if (mode != FoldersApiRoutingMode.V2Only)
        {
            app.UseHistoricalMutationAuthorization();
        }
        app.MapSubscribeHandler().RequireEventStoreSidecarChannel();
        app.MapFoldersServerEndpoints();
        app.MapEventStoreDomainProjectionEndpoints();
        app.MapEventStoreDomainProjectionMetadataEndpoint();
        app.MapEventStoreRouteCatalog();
        app.UseEventStoreDevelopmentDomainCatalog();

        return app;
    }
}
