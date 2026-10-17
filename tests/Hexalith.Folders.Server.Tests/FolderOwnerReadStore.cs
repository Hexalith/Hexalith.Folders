using System.Text.Json;
using Hexalith.EventStore.Client.Projections;
namespace Hexalith.Folders.Server.Tests;
internal sealed class FolderOwnerReadStore : IReadModelStore
{
    private readonly Dictionary<string, (byte[] Json, int Version)> values = new(StringComparer.Ordinal);
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
        => Task.FromResult(values.TryGetValue(key, out var row) ? new ReadModelEntry<T>(JsonSerializer.Deserialize<T>(row.Json), row.Version.ToString()) : new(null, null));
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class => throw new NotSupportedException();
    public Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
    {
        int version = values.TryGetValue(key, out var row) ? row.Version : 0;
        if (etag != (version == 0 ? "" : version.ToString())) return Task.FromResult(false);
        values[key] = (JsonSerializer.SerializeToUtf8Bytes(value), version + 1); return Task.FromResult(true);
    }
}
