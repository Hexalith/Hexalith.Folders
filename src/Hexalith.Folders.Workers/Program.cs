using Hexalith.Folders.ServiceDefaults;
using Hexalith.Folders.Workers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// "When services run" includes workers: bring the workers host into the observability/health pipeline
// (OpenTelemetry traces/metrics/logs export plus liveness/readiness probes) like the server host.
builder.AddFoldersWorkersHost();

WebApplication app = builder.Build();

app.UseFoldersWorkersPipeline();

await app.RunAsync().ConfigureAwait(false);
