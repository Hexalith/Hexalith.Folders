using System.Runtime.CompilerServices;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Replays the actual serialized owner chunks through the named handler's streaming seam.</summary>
internal sealed class OperationalStatusPartitionCursor(IReadOnlyList<ReadOnlyMemory<byte>> chunks)
    : IDomainSharedProjectionPartitionCursor
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<DomainSharedProjectionRebuildPartition> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        for (int index = 0; index < chunks.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new(0, "folder-a", chunks[index], index);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<DomainSharedProjectionRebuildPartition>> ReadRangeAsync(long ownerOrdinal,
        int firstChunkOrdinal, int chunkCount, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> TryWriteScratchAsync<TValue>(string key, TValue value, string batchId,
        ReadModelBatchConcurrency concurrency, CancellationToken cancellationToken) where TValue : class
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public string ScratchMarkerKey(string batchId) => throw new NotSupportedException();
}
