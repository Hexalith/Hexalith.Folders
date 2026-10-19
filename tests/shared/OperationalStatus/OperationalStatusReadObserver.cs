using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Folders.Tests.OperationalStatus;

/// <summary>Observes store access while delegating persistence to the existing platform harness.</summary>
internal sealed class OperationalStatusReadObserver(IReadModelStore inner, Func<string, Task>? afterRead = null) : IReadModelStore
{
    /// <summary>Gets the number of store observations.</summary>
    public int Observations { get; private set; }

    /// <inheritdoc/>
    public async Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default)
        where TValue : class
    {
        Observations++;
        var row = await inner.GetAsync<TValue>(storeName, key, cancellationToken).ConfigureAwait(false);
        if (afterRead is not null) await afterRead(key).ConfigureAwait(false);
        return row;
    }

    /// <inheritdoc/>
    public Task SaveAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default)
        where TValue : class
    {
        Observations++;
        return inner.SaveAsync(storeName, key, value, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default)
        where TValue : class
    {
        Observations++;
        return inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
    }
}
