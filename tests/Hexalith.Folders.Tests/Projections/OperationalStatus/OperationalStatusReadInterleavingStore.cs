using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Tests.OperationalStatus;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

/// <summary>Runs one actual SDK write between the reader's pinned multi-key observations.</summary>
internal sealed class OperationalStatusReadInterleavingStore(OperationalStatusTestStore inner, string triggerKey, Func<Task> mutation) : IReadModelStore
{
    private Func<Task>? _mutation = mutation;

    /// <inheritdoc/>
    public async Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default) where TValue : class
    {
        if (key == triggerKey && Interlocked.Exchange(ref _mutation, null) is { } run)
            await run().ConfigureAwait(false);
        return await inner.GetAsync<TValue>(storeName, key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task SaveAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default) where TValue : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default) where TValue : class
        => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
}
