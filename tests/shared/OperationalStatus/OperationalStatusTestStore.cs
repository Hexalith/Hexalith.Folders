using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Folders.Projections.OperationalStatus;

namespace Hexalith.Folders.Tests.OperationalStatus;

/// <summary>Inspects and corrupts task-owned selected physical values while running the real SDK batch engine.</summary>
internal sealed class OperationalStatusTestStore : IReadModelStore, IReadModelBatchStore, IReadModelBatchStagingStore,
    IReadModelPendingBatchRecoveryStore, IReadModelConditionalEraser
{
    public InMemoryReadModelStore Inner { get; } = new();
    public int Count => Inner.Count;
    public TimeSpan? LastTimeToLive => Inner.LastTimeToLive;
    public ReadModelBatchOptions BatchOptions => Inner.BatchOptions;
    public Func<ReadModelBatchPhase, int, CancellationToken, Task>? BatchFaultHook { get => Inner.BatchFaultHook; set => Inner.BatchFaultHook = value; }
    public Action? ConcurrentWriteBeforeTrySave { get => Inner.ConcurrentWriteBeforeTrySave; set => Inner.ConcurrentWriteBeforeTrySave = value; }
    public string PhysicalKey(string key)
    {
        if (!key.StartsWith("projection:folders:operational-status:", StringComparison.Ordinal)) return key;
        var scope = OperationalStatusGeneration.Scope("tenant-a");
        JsonObject? state = Inner.Snapshot<JsonObject>("statestore", "shared-epoch:" + scope.ComputeHash());
        long generation = state?["activeGeneration"]?.GetValue<long>() ?? 0;
        return "shared-generation:" + scope.ComputeHash() + ":" + generation + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }
    public T? Snapshot<T>(string store, string key) where T : class => Inner.Snapshot<T>(store, PhysicalKey(key));
    public void SeedRaw<T>(string store, string key, T value) where T : class => Inner.SeedRaw(store, PhysicalKey(key), value);
    public bool HasPendingEnvelope(string store, string key) => Inner.HasPendingEnvelope(store, PhysicalKey(key));
    public Task<ReadModelEntry<T>> GetAsync<T>(string store, string key, CancellationToken token = default) where T : class => Inner.GetAsync<T>(store, PhysicalKey(key), token);
    public Task SaveAsync<T>(string store, string key, T value, CancellationToken token = default) where T : class => Inner.SaveAsync(store, PhysicalKey(key), value, token);
    public Task<bool> TrySaveAsync<T>(string store, string key, T value, string etag, CancellationToken token = default) where T : class => Inner.TrySaveAsync(store, PhysicalKey(key), value, etag, token);
    public Task<(bool Present, string Etag)> TryReadEtagAsync(string store, string key, CancellationToken token = default) => Inner.TryReadEtagAsync(store, PhysicalKey(key), token);
    public Task<bool> TryEraseAsync(string store, string key, string etag, CancellationToken token = default) => Inner.TryEraseAsync(store, PhysicalKey(key), etag, token);
    public Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken token = default) => Inner.ExecuteAsync(batch, token);
    public Task<ReadModelBatchStagingResult> StageAsync(ReadModelBatch batch, CancellationToken token = default) => Inner.StageAsync(batch, token);
    public Task<ReadModelBatchStagingResult> CommitAsync(ReadModelBatch batch, CancellationToken token = default) => Inner.CommitAsync(batch, token);
    public Task<ReadModelBatchStagingResult> AbortAsync(ReadModelBatch batch, CancellationToken token = default) => Inner.AbortAsync(batch, token);
    public Task<ReadModelBatchStagingResult> VerifyAsync(ReadModelBatch batch, CancellationToken token = default) => Inner.VerifyAsync(batch, token);
    public Task<ReadModelPendingBatchRecoveryResult> RecoverPendingAsync(ReadModelBatchScope scope, string key, CancellationToken token = default) => Inner.RecoverPendingAsync(scope, PhysicalKey(key), token);
}
