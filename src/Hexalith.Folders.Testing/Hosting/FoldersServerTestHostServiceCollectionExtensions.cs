using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Testing;

public static class FoldersServerTestHostServiceCollectionExtensions
{
    public static IServiceCollection AddFoldersServerTestDefaults(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Slim test hosts do not call AddServiceDefaults, but AddFoldersServer registers
        // FoldersAuthSchemeValidator and MapFoldersServerEndpoints maps health endpoints.
        services.AddAuthentication();
        services.AddHealthChecks();

        return services;
    }
}
