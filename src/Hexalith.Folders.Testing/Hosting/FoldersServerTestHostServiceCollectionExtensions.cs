using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Testing;

/// <summary>Composes isolated domain-handler fixtures with synthetic human transport authentication.</summary>
/// <remarks>This helper supplies no production JWT, channel or route-catalog conformance evidence.</remarks>
public static class FoldersServerTestHostServiceCollectionExtensions
{
    /// <summary>Registers a synthetic transport principal while preserving the fixture's separately supplied domain authority.</summary>
    /// <param name="services">The isolated fixture services.</param>
    /// <returns>The service collection.</returns>
    /// <remarks>Production hosts must use their production security composition; this helper does not validate credentials.</remarks>
    public static IServiceCollection AddFoldersServerTestDefaults(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Slim test hosts do not call AddServiceDefaults, but AddFoldersServer registers
        // FoldersAuthSchemeValidator and MapFoldersServerEndpoints maps health endpoints.
        const string fixtureScheme = "FoldersIsolatedHuman";
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, FoldersIsolatedHumanAuthenticationHandler>(fixtureScheme, static _ => { });
        services.PostConfigure<AuthenticationOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.DefaultScheme))
            {
                options.DefaultAuthenticateScheme ??= fixtureScheme;
                options.DefaultChallengeScheme ??= fixtureScheme;
            }
        });
        // This helper isolates domain handlers from transport validation. Production hosts do not call it.
        // Keep the SDK human credential-kind requirements; only the explicitly fake fixture scheme differs.
        services.AddAuthorization(options => options.AddPolicy(EventStoreHttpHostSecurityExtensions.HumanPolicy,
            new AuthorizationPolicy(EventStoreHttpHostSecurityExtensions.CreateHumanPolicy().Requirements, [fixtureScheme])));
        services.AddHealthChecks();

        return services;
    }
}
