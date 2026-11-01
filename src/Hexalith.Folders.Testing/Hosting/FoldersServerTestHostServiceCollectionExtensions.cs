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
        => services.AddFoldersServerTestDefaults(null);

    /// <summary>Registers isolated transport defaults with the selected synthetic human authentication scheme.</summary>
    /// <param name="services">The isolated fixture services.</param>
    /// <param name="humanAuthenticationScheme">An existing synthetic human scheme, or <see langword="null"/> to use the isolated handler.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentException">The supplied scheme name is empty or whitespace.</exception>
    /// <remarks>Call after SDK host registration. This helper does not validate production credentials.</remarks>
    public static IServiceCollection AddFoldersServerTestDefaults(this IServiceCollection services, string? humanAuthenticationScheme)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (humanAuthenticationScheme is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(humanAuthenticationScheme);
        }

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
        services.AddAuthorization();
        // Register after the SDK host services when composing a routing fixture so its
        // post-configuration cannot replace the synthetic scheme with Bearer.
        services.PostConfigure<AuthorizationOptions>(options => options.AddPolicy(EventStoreHttpHostSecurityExtensions.HumanPolicy,
            new AuthorizationPolicy(EventStoreHttpHostSecurityExtensions.CreateHumanPolicy().Requirements, [humanAuthenticationScheme ?? fixtureScheme])));
        services.AddHealthChecks();

        return services;
    }
}
