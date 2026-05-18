using Hexalith.EventStore.Client.Registration;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.Server;

public static class FoldersServerServiceCollectionExtensions
{
    public static IServiceCollection AddFoldersDomainServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddEventStore(typeof(FoldersModule).Assembly);
        services.AddHttpContextAccessor();
        services.AddOptions<TenantContextOptions>();
        // Register the authentication core services so the validator can introspect scheme registrations.
        // Concrete schemes (JWT bearer, OIDC) are added by the host composition (Story 7.2).
        _ = services.AddAuthentication();
        services.TryAddSingleton<ITenantContextAccessor, HttpContextTenantContextAccessor>();
        services.TryAddScoped<FoldersDomainServiceRequestHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, FoldersAuthSchemeValidator>());

        return services;
    }
}
