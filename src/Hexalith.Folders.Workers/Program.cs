using Hexalith.Folders;
using Hexalith.Folders.ServiceDefaults;
using Hexalith.Folders.Workers;
using Hexalith.Folders.Projections.TenantAccess;
using Microsoft.Extensions.DependencyInjection.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// "When services run" includes workers: bring the workers host into the observability/health pipeline
// (OpenTelemetry traces/metrics/logs export plus liveness/readiness probes) like the server host.
builder.AddServiceDefaults();
builder.Services.AddFoldersTenantEventWorkers();
builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
builder.Services.AddSingleton<IFolderTenantAccessProjectionStore, EventStoreFolderTenantAccessProjectionStore>();

WebApplication app = builder.Build();

app.UseCloudEvents();
app.MapSubscribeHandler();
app.MapFoldersTenantEventWorkerEndpoints();
app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);
