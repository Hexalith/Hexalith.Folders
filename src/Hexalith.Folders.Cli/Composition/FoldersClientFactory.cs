using System;

using Hexalith.Folders.Client;
using Hexalith.Folders.Client.Generated;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Cli.Composition;

/// <summary>
/// Production factory that composes a typed <see cref="IClient"/> from the resolved base address and bearer
/// token. It reuses <c>AddFoldersClient</c> (no duplicated wiring) and attaches the bearer token via a
/// <see cref="BearerTokenHandler"/> on the returned HTTP pipeline — the CLI does not invent an auth scheme.
/// </summary>
internal static class FoldersClientFactory
{
    /// <summary>Builds an <see cref="IClient"/> for a single CLI invocation.</summary>
    /// <param name="baseAddress">The absolute Folders REST base address.</param>
    /// <param name="token">The resolved, non-blank bearer token.</param>
    /// <returns>A configured typed client.</returns>
    public static IClient Create(Uri baseAddress, string token)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        ServiceCollection services = new();
        _ = services
            .AddFoldersClient(options => options.BaseAddress = baseAddress)
            .AddHttpMessageHandler(() => new BearerTokenHandler(token));

        // The provider lives for the duration of the process (one command per invocation); the OS reclaims
        // the HttpClient on exit. Disposing here would dispose the client before the awaited call completes.
        ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IClient>();
    }
}
