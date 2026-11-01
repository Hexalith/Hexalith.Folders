using Hexalith.EventStore;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.HealthChecks;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.OpenApi;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.SignalRHub;
using Hexalith.Folders.EventStore;

using Microsoft.AspNetCore.Server.Kestrel.Core;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddDaprClient();
builder.Services.AddHealthChecks()
    .AddEventStoreDaprHealthChecks()
    .AddEventStoreAppChannelTokenHealthCheck();
builder.Services.AddExceptionHandler<FoldersDomainServiceSafeDenialExceptionHandler>();
builder.Services.AddEventStore();
builder.Services.AddEventStoreServer(builder.Configuration);
if (string.IsNullOrEmpty(builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:CatalogPath"]))
    builder.Services.AddFoldersIdempotencyIntentAdapters();
builder.Services.AddEventStoreDomainQueryRouting();
builder.Services.AddEventStoreSignalR(builder.Configuration);

WebApplication app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();

if (app.Configuration.GetValue("EventStore:OpenApi:Enabled", true))
{
    _ = app.MapOpenApi().RequireAuthorization(EventStoreHttpHostSecurityExtensions.HumanPolicy);
    _ = app.UseEventStoreHumanSurface("/swagger");
    _ = app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Hexalith EventStore API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseEventStoreHttpHostSecurity();
app.UseAuthorization();
if (!string.IsNullOrEmpty(app.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:CatalogPath"]))
{
    Hexalith.EventStore.ServiceDefaults.Routing.FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app,
        Hexalith.EventStore.Server.Routing.RunScopedRouteCatalogGateway.Operation, technical: true);
}
app.UseCloudEvents();

bool declarativeSubscriptions = app.Configuration.GetValue<bool>("Dapr:DeclarativeSubscriptions");
app.MapControllers().WithEventStoreProjectionNotifications(app.Services, declarativeSubscriptions);
app.MapErrorReferences();
app.MapApiVersionFallback();
if (!declarativeSubscriptions)
{
    app.MapSubscribeHandler().RequireEventStoreChannel();
}
app.MapActorsHandlers()
    .WithMetadata(EventStoreActorCallbackEndpointMetadata.Instance)
    .RequireEventStoreChannel();
app.MapApplicationRuntimeProofEndpoints();

SignalROptions? signalROptions = app.Configuration
    .GetSection("EventStore:SignalR")
    .Get<SignalROptions>();

if (signalROptions?.Enabled == true)
{
    _ = app.MapHub<ProjectionChangedHub>(ProjectionChangedHub.HubPath)
        .RequireAuthorization(EventStoreHttpHostSecurityExtensions.HumanPolicy);
    _ = app.MapSignalRRuntimeProofEndpoints();
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    KestrelServerOptions? kestrelOptions = app.Services.GetService<KestrelServerOptions>();
    _ = kestrelOptions?.Limits.MaxRequestBodySize = 1_048_576;
});

app.Run();

/// <summary>
/// Folders EventStore command-host entry point.
/// </summary>
public partial class Program;
