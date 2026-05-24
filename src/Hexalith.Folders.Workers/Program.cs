using Hexalith.Folders;
using Hexalith.Folders.Workers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddFoldersTenantEventWorkers();

WebApplication app = builder.Build();

app.UseCloudEvents();
app.MapSubscribeHandler();
app.MapFoldersTenantEventWorkerEndpoints();

await app.RunAsync().ConfigureAwait(false);
