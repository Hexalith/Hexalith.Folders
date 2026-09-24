using Hexalith.Folders.Aggregates.Folder;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.Server;

/// <summary>
/// Asserts at startup that an <see cref="IFolderRepository"/> is registered. Without this, a misconfigured
/// production composition would only fail on the first request with an opaque NRE. Throwing during
/// <see cref="StartAsync"/> prevents the host from accepting traffic.
/// </summary>
/// <param name="services">The root service provider.</param>
/// <param name="environment">The host environment.</param>
internal sealed class FolderRepositoryStartupAssertion(IServiceProvider services, IHostEnvironment environment) : IHostedService
{
    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
