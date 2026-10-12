using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Projections.TenantAccess;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class EventStoreFolderTenantAccessProjectionStoreTests
{
    [Fact]
    public async Task SeparateHostsObserveTheSameProjectionAndRejectAStaleWrite()
    {
        MemoryReadModels readModels = new();
        EventStoreFolderTenantAccessProjectionStore worker = new(readModels);
        EventStoreFolderTenantAccessProjectionStore server = new(readModels);
        FolderTenantAccessProjection initial = new() { TenantId = "tenant-a", Enabled = true };

        await worker.SaveAsync(initial, TestContext.Current.CancellationToken);
        FolderTenantAccessProjection serverRead = (await server.GetAsync("tenant-a", TestContext.Current.CancellationToken))!;
        serverRead.ShouldNotBeNull();
        serverRead.Enabled.ShouldBeTrue();
        serverRead.Version.ShouldBe(1);

        FolderTenantAccessProjection workerRead = (await worker.GetAsync("tenant-a", TestContext.Current.CancellationToken))!;
        workerRead.Enabled = false;
        await worker.SaveAsync(workerRead, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<TenantAccessConcurrencyException>(
            () => server.SaveAsync(serverRead, TestContext.Current.CancellationToken));
        (await server.GetAsync("tenant-a", TestContext.Current.CancellationToken))!.Enabled.ShouldBeFalse();
    }

    private sealed class MemoryReadModels : IReadModelStore
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<string, (byte[] Json, int Version)> _values = new(StringComparer.Ordinal);

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
                if (!string.Equals(etag, current == 0 ? string.Empty : current.ToString(), StringComparison.Ordinal))
                {
                    return Task.FromResult(false);
                }

                _values[key] = (JsonSerializer.SerializeToUtf8Bytes(value), current + 1);
                return Task.FromResult(true);
            }
        }
    }
}
