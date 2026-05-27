using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Folders.Client.Convenience;

/// <summary>
/// Registration helpers for the optional SDK convenience abstractions, layered on top of
/// <see cref="FoldersClientServiceCollectionExtensions.AddFoldersClient(IServiceCollection)"/> without
/// altering the generated client registration or method signatures.
/// </summary>
public static class FoldersConvenienceServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="ICorrelationIdProvider"/> implementation that
    /// <see cref="CorrelationAndTaskId.ResolveCorrelationId(string?, ICorrelationIdProvider?)"/> can consult
    /// before the SDK ULID fallback. The provider never affects task-ID or idempotency-key sourcing.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddFoldersCorrelationIdProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ICorrelationIdProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ICorrelationIdProvider, TProvider>();
        return services;
    }

    /// <summary>
    /// Registers a specific <see cref="ICorrelationIdProvider"/> instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="provider">The provider instance.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddFoldersCorrelationIdProvider(this IServiceCollection services, ICorrelationIdProvider provider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(provider);

        services.TryAddSingleton(provider);
        return services;
    }
}
