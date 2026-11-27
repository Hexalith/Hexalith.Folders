using System.Collections.Concurrent;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Observes the actual SDK store reads and can refuse one protected view family.</summary>
internal sealed class McpCliGatewayQueryReadModelStore(DaprReadModelStore inner) : IReadModelStore
{
    /// <summary>Records selected keys without substituting a different persisted value.</summary>
    internal ConcurrentQueue<string> Reads { get; } = new();

    /// <summary>Refuses the configured key fragment to simulate store unavailability.</summary>
    internal string? UnavailableKey { get; set; }

    /// <inheritdoc/>
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
    {
        Reads.Enqueue(key);
        return UnavailableKey is { } fragment && key.Contains(fragment, StringComparison.Ordinal)
            ? throw new IOException("protected fixture details")
            : inner.GetAsync<T>(storeName, key, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ReadModelEntry<T>> GetCompactedAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
        => inner.GetCompactedAsync<T>(storeName, key, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
        => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
}
