using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.Server;

/// <summary>
/// Asserts at startup that EventStore actor-folded repositories are registered. Throwing during
/// <see cref="StartAsync"/> prevents the host from accepting traffic.
/// </summary>
/// <param name="services">The root service provider.</param>
internal sealed class FolderRepositoryStartupAssertion(IServiceProvider services) : IHostedService
{
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = services.CreateScope();
        if (scope.ServiceProvider.GetService<IFolderRepository>() is not ActorFoldedFolderRepository)
        {
            throw new InvalidOperationException("The EventStore domain-service host requires the actor-folded folder repository.");
        }

        if (scope.ServiceProvider.GetService<IOrganizationProviderBindingRepository>()
                is not ActorFoldedOrganizationProviderBindingRepository)
        {
            throw new InvalidOperationException("The EventStore domain-service host requires the actor-folded organization repository.");
        }

        if (scope.ServiceProvider.GetService<IOrganizationAclRepository>()
                is not ActorFoldedOrganizationProviderBindingRepository)
        {
            throw new InvalidOperationException("The EventStore domain-service host requires the actor-folded organization ACL repository.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
