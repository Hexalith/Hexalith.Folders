using Hexalith.EventStore;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.HealthChecks;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.OpenApi;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.SignalRHub;
using Hexalith.Folders.EventStore;

using Microsoft.AspNetCore.Server.Kestrel.Core;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddDaprClient();
builder.Services.AddHealthChecks()
    .AddEventStoreDaprHealthChecks();
builder.Services.AddEventStore();
builder.Services.AddEventStoreServer(builder.Configuration);
builder.Services.AddFoldersIdempotencyIntentAdapters();
builder.Services.AddEventStoreDomainQueryRouting();
builder.Services.AddEventStoreSignalR(builder.Configuration);

WebApplication app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseCloudEvents();

if (app.Configuration.GetValue("EventStore:OpenApi:Enabled", true))
{
    _ = app.MapOpenApi();
    _ = app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Hexalith EventStore API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapControllers();
app.MapErrorReferences();
app.MapApiVersionFallback();
app.MapSubscribeHandler();
app.MapActorsHandlers();
app.MapApplicationRuntimeProofEndpoints();

SignalROptions? signalROptions = app.Configuration
    .GetSection("EventStore:SignalR")
    .Get<SignalROptions>();

if (signalROptions?.Enabled == true)
{
    _ = app.MapHub<ProjectionChangedHub>(ProjectionChangedHub.HubPath);
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
