using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.Folders.Workers.Tests;

/// <summary>Observes permission tests while using the shared platform persistence harness.</summary>
internal sealed class WorkerRouteReadModelStore : IReadModelStore, IReadModelConditionalEraser,
    IReadModelBatchStore, IReadModelPendingBatchRecoveryStore
{
    private readonly InMemoryReadModelStore _store = new();
    private readonly Lock _lock = new();
    public Action<string>? ReadObserved { get; set; }
    public int ForcedWriteConflicts { get; set; }
    public bool FailOperationalBatch { get; set; }
    public void Remove(string key)
    {
        var current = _store.TryReadEtagAsync("statestore", key).GetAwaiter().GetResult();
        if (current.Present) _store.TryEraseAsync("statestore", key, current.Etag).GetAwaiter().GetResult();
    }
    public bool TryRemove(string key, string expectedEtag)
        => _store.TryEraseAsync("statestore", key, expectedEtag).GetAwaiter().GetResult();
    public Task<bool> TryEraseAsync(string storeName, string key, string etag, CancellationToken cancellationToken = default)
        => _store.TryEraseAsync(storeName, key, etag, cancellationToken);
    public Task<(bool Present, string Etag)> TryReadEtagAsync(string storeName, string key, CancellationToken cancellationToken = default)
        => _store.TryReadEtagAsync(storeName, key, cancellationToken);
    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default) where TValue : class
    {
        ReadObserved?.Invoke(key);
        return _store.GetAsync<TValue>(storeName, key, cancellationToken);
    }
    public Task SaveAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default) where TValue : class
        => _store.SaveAsync(storeName, key, value, cancellationToken);
    public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default) where TValue : class
    {
        lock (_lock)
        {
            if (ForcedWriteConflicts > 0)
            {
                ForcedWriteConflicts--;
                return Task.FromResult(false);
            }
        }
        return _store.TrySaveAsync(storeName, key, value, etag, cancellationToken);
    }
    public Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
        => FailOperationalBatch ? Task.FromResult(ReadModelBatchResult.Indeterminate(string.Empty, "injected_operational_unavailable"))
            : _store.ExecuteAsync(batch, cancellationToken);
    public Task<ReadModelPendingBatchRecoveryResult> RecoverPendingAsync(ReadModelBatchScope scope, string key,
        CancellationToken cancellationToken = default) => _store.RecoverPendingAsync(scope, key, cancellationToken);
}
