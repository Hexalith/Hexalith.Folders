using Hexalith.Folders;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Contracts;
using Hexalith.Folders.Server;
using Hexalith.Folders.ServiceDefaults;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddFoldersServer();
builder.Services.AddFoldersProductionAuthentication(builder.Configuration, builder.Environment);

// Production safety gate: AddInMemoryFolderRepository registers a dictionary-backed
// repository that loses every event on process restart. It is safe for dev/staging hosts
// and integration tests; it is not safe for production. Gate the call on environment so a
// production build that forgets to register an EventStore-backed repository fails at
// startup (via FolderRepositoryStartupAssertion) rather than silently running on a
// dictionary. Remove this gate and the assertion together when Epic 7 wires the
// EventStore-backed repository as the production default.
if (builder.Environment.IsDevelopment() || builder.Environment.IsStaging())
{
    // TODO Epic 7: replace AddInMemoryFolderRepository with an EventStore-backed
    // repository for all environments.
    builder.Services.AddInMemoryFolderRepository();
}

builder.Services.AddHostedService<FolderRepositoryStartupAssertion>();

// Validate scoped service lifetimes at build time so a missing or misconfigured DI
// registration (e.g., a forgotten ILayeredFolderAuthorizationResultAccessor or a captive
// dependency) fails at host startup rather than on the first request.
builder.Host.UseDefaultServiceProvider(static options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

WebApplication app = builder.Build();

app.UseCloudEvents();
app.UseAuthentication();
app.UseAuthorization();
app.MapSubscribeHandler();
app.MapFoldersServerEndpoints();

app.Run();

// Hosted service that asserts at startup that an IFolderRepository is registered. Without
// this, a misconfigured production composition would only fail on the first request with
// an opaque NRE. Throwing during StartAsync prevents the host from accepting traffic.
file sealed class FolderRepositoryStartupAssertion(IServiceProvider services, IHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        IFolderRepository? repository = services.GetService<IFolderRepository>();
        if (repository is null)
        {
            throw new InvalidOperationException(
                $"No IFolderRepository is registered. Production hosts must register an EventStore-backed implementation; dev/staging hosts must call AddInMemoryFolderRepository(). Environment: '{environment.EnvironmentName}'.");
        }

        if (environment.IsProduction() && repository is InMemoryFolderRepository)
        {
            throw new InvalidOperationException(
                "InMemoryFolderRepository is registered in a Production environment. The in-memory implementation loses all events on process restart and is not safe for production. Register an EventStore-backed IFolderRepository instead.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
