using System;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client;
using Hexalith.Folders.Client.Generated;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Sample;

/// <summary>
/// Composition root for the Folders SDK sample. Demonstrates the recommended DI registration:
/// <c>AddFoldersClient</c> for the typed <see cref="IClient"/> plus a bearer-token
/// <see cref="DelegatingHandler"/> chained onto the returned HTTP client builder.
/// </summary>
public static class FoldersSampleHost
{
    /// <summary>
    /// Builds a service provider with the Folders typed client registered against the supplied base address,
    /// chaining a <see cref="BearerTokenHandler"/> that sources its token from <paramref name="tokenFactory"/>.
    /// </summary>
    /// <param name="baseAddress">The absolute base address of the Folders server (for example, the AppHost endpoint).</param>
    /// <param name="tokenFactory">A delegate that returns a bearer token, or a blank value to run unauthenticated.</param>
    /// <returns>A configured <see cref="ServiceProvider"/>; dispose it when finished.</returns>
    public static ServiceProvider BuildServiceProvider(
        Uri baseAddress,
        Func<CancellationToken, ValueTask<string?>>? tokenFactory = null)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        ServiceCollection services = new();

        Func<CancellationToken, ValueTask<string?>> resolvedTokenFactory =
            tokenFactory ?? (static _ => ValueTask.FromResult<string?>(null));

        services.AddTransient(_ => new BearerTokenHandler(resolvedTokenFactory));

        _ = services
            .AddFoldersClient(options => options.BaseAddress = baseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>();

        return services.BuildServiceProvider();
    }
}
