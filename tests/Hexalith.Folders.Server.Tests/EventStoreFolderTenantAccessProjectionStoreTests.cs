using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Authorization;

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

    [Theory]
    [InlineData(FolderTenantAccessEventKind.TenantDisabled)]
    [InlineData(FolderTenantAccessEventKind.UserRemovedFromTenant)]
    public async Task RetryingHandlerPreservesRevocationAfterActualAdapterConditionalWriteConflict(FolderTenantAccessEventKind kind)
    {
        MemoryReadModels readModels = new();
        EventStoreFolderTenantAccessProjectionStore adapter = new(readModels);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        FolderTenantAccessProjection initial = new()
        {
            TenantId = "tenant-a", Enabled = true, Watermark = 1,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>
            {
                ["user-a"] = new("user-a", "TenantOwner"),
            },
            LastEventTimestamp = now, ProjectionWatermark = "tenant-a:1",
        };
        CancellationToken ct = TestContext.Current.CancellationToken;
        await adapter.SaveAsync(initial, ct);
        FolderTenantAccessProjection competing = (await adapter.GetAsync("tenant-a", ct))!;
        competing.Principals["user-b"] = new("user-b", "TenantReader");
        competing.Watermark = 2;
        competing.ProjectionWatermark = "tenant-a:2";
        competing.Version = 2;
        readModels.BeforeConditionalWrite = () => readModels.PersistCompetingProjection(competing);
        FolderTenantAccessHandler handler = new(adapter, new FixedUtcClock(now),
            new TenantAccessOptions { ConcurrencyRetryAttempts = 2 });
        FolderTenantAccessEvent revoke = new(kind, "tenant-a", "message-revoke", 3, now,
            "correlation-a", kind == FolderTenantAccessEventKind.UserRemovedFromTenant ? "user-a" : null,
            PayloadFingerprint: "revoke-fingerprint");

        await handler.HandleAsync(revoke, ct);

        readModels.FailedConditionalWrites.ShouldBe(1);
        FolderTenantAccessProjection persisted = (await new EventStoreFolderTenantAccessProjectionStore(readModels)
            .GetAsync("tenant-a", ct))!;
        persisted.Version.ShouldBe(3);
        persisted.Watermark.ShouldBe(3);
        persisted.ProjectionWatermark.ShouldBe("tenant-a:3");
        persisted.ProcessedMessages.ShouldContainKey("message-revoke");
        persisted.Principals.ShouldContainKey("user-b");
        if (kind == FolderTenantAccessEventKind.TenantDisabled)
        {
            persisted.Enabled.ShouldBeFalse();
        }
        else
        {
            persisted.Principals.ShouldNotContainKey("user-a");
        }
    }

    private sealed class MemoryReadModels : IReadModelStore
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<string, (byte[] Json, int Version)> _values = new(StringComparer.Ordinal);

        public Action? BeforeConditionalWrite { get; set; }

        public int FailedConditionalWrites { get; private set; }

        public void PersistCompetingProjection(FolderTenantAccessProjection projection)
        {
            const string key = "projection:folders:tenant-access:tenant-a";
            int version = _values[key].Version + 1;
            _values[key] = (JsonSerializer.SerializeToUtf8Bytes(projection), version);
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
                Action? competingWrite = BeforeConditionalWrite;
                BeforeConditionalWrite = null;
                competingWrite?.Invoke();
                int current = _values.TryGetValue(key, out var entry) ? entry.Version : 0;
                if (!string.Equals(etag, current == 0 ? string.Empty : current.ToString(), StringComparison.Ordinal))
                {
                    FailedConditionalWrites++;
                    return Task.FromResult(false);
                }

                _values[key] = (JsonSerializer.SerializeToUtf8Bytes(value), current + 1);
                return Task.FromResult(true);
            }
        }
    }
}
