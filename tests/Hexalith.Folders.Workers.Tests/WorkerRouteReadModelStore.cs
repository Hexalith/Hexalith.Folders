using System.Text.Json;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Folders.Workers.Tests;

internal sealed class WorkerRouteReadModelStore : IReadModelStore, IReadModelConditionalEraser
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, (byte[] Json, int Version)> _values = new(StringComparer.Ordinal);

    public void Remove(string key)
    {
        lock (_lock)
        {
            _ = _values.Remove(key);
        }
    }

    public bool TryRemove(string key, string expectedEtag)
    {
        lock (_lock)
        {
            if (!_values.TryGetValue(key, out var entry))
            {
                return true;
            }

            return entry.Version.ToString() == expectedEtag && _values.Remove(key);
        }
    }

    public Task<bool> TryEraseAsync(string storeName, string key, string etag,
        CancellationToken cancellationToken = default)
        => Task.FromResult(TryRemove(key, etag));

    public Task<(bool Present, string Etag)> TryReadEtagAsync(string storeName, string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return Task.FromResult(_values.TryGetValue(key, out var entry)
                ? (true, entry.Version.ToString()) : (false, string.Empty));
        }
    }

    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key,
        CancellationToken cancellationToken = default) where TValue : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return Task.FromResult(_values.TryGetValue(key, out var entry)
                ? new ReadModelEntry<TValue>(JsonSerializer.Deserialize<TValue>(entry.Json), entry.Version.ToString())
                : new ReadModelEntry<TValue>(null, null));
        }
    }

    public Task SaveAsync<TValue>(string storeName, string key, TValue value,
        CancellationToken cancellationToken = default) where TValue : class
        => throw new NotSupportedException();

    public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag,
        CancellationToken cancellationToken = default) where TValue : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            int current = _values.TryGetValue(key, out var entry) ? entry.Version : 0;
            if (etag != (current == 0 ? string.Empty : current.ToString()))
            {
                return Task.FromResult(false);
            }

            _values[key] = (JsonSerializer.SerializeToUtf8Bytes(value), current + 1);
            return Task.FromResult(true);
        }
    }
}
