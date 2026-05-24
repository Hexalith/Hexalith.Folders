using Hexalith.Folders.Client.Generated;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// Disambiguate the generated type from the enclosing "Hexalith.Folders.Client" namespace.
using GeneratedFoldersClient = Hexalith.Folders.Client.Generated.Client;

namespace Hexalith.Folders.Client;

/// <summary>
/// Extension methods that register the Hexalith.Folders typed SDK client (<see cref="IClient"/>) as a
/// typed <see cref="System.Net.Http.HttpClient"/> in the dependency injection container.
/// </summary>
/// <remarks>
/// Authentication is deliberately out of scope: the returned <see cref="IHttpClientBuilder"/> lets callers
/// attach a bearer-token <see cref="System.Net.Http.DelegatingHandler"/> (recommended) without this module
/// taking a dependency on any particular token-acquisition strategy.
/// </remarks>
public static class FoldersClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Folders typed client, binding <see cref="FoldersClientOptions"/> from the
    /// <see cref="FoldersClientOptions.DefaultConfigurationSectionName"/> configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>An <see cref="IHttpClientBuilder"/> so callers can chain message handlers (for example, a bearer-token handler).</returns>
    public static IHttpClientBuilder AddFoldersClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<FoldersClientOptions>()
            .BindConfiguration(FoldersClientOptions.DefaultConfigurationSectionName);

        return services.AddConfiguredFoldersClient();
    }

    /// <summary>
    /// Registers the Folders typed client with explicit <see cref="FoldersClientOptions"/> configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate that configures <see cref="FoldersClientOptions"/>.</param>
    /// <returns>An <see cref="IHttpClientBuilder"/> so callers can chain message handlers (for example, a bearer-token handler).</returns>
    public static IHttpClientBuilder AddFoldersClient(
        this IServiceCollection services,
        Action<FoldersClientOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddOptions<FoldersClientOptions>();
        _ = services.Configure(configureOptions);

        return services.AddConfiguredFoldersClient();
    }

    private static IHttpClientBuilder AddConfiguredFoldersClient(this IServiceCollection services)
    {
        // Fail fast (at first resolve) when the transport endpoint is missing or relative.
        services
            .AddOptions<FoldersClientOptions>()
            .Validate(
                static options => options.BaseAddress is not null,
                $"{nameof(FoldersClientOptions)}.{nameof(FoldersClientOptions.BaseAddress)} must be configured.")
            .Validate(
                static options => options.BaseAddress is null || options.BaseAddress.IsAbsoluteUri,
                $"{nameof(FoldersClientOptions)}.{nameof(FoldersClientOptions.BaseAddress)} must be an absolute URI.");

        return services.AddHttpClient<IClient, GeneratedFoldersClient>(static (serviceProvider, httpClient) =>
        {
            FoldersClientOptions options = serviceProvider
                .GetRequiredService<IOptions<FoldersClientOptions>>()
                .Value;
            httpClient.BaseAddress = options.BaseAddress;
        });
    }
}
