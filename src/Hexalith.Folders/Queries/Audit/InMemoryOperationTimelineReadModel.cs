using System.Collections.Concurrent;

using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Queries.Audit;

public sealed class InMemoryOperationTimelineReadModel(IUtcClock clock) : IOperationTimelineReadModel
{
    private readonly ConcurrentDictionary<ScopedKey, OperationTimelineReadModelSnapshot> _snapshots = new();
    private readonly IUtcClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private int _getCount;

    public int GetCount => _getCount;

    public void Save(OperationTimelineReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.FolderId);
        _snapshots[new ScopedKey(snapshot.ManagedTenantId, snapshot.FolderId)] = snapshot;
    }

    public Task<OperationTimelineReadModelResult> GetAsync(
        OperationTimelineReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FolderId);
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _getCount);

        return Task.FromResult(_snapshots.TryGetValue(new ScopedKey(request.ManagedTenantId, request.FolderId), out OperationTimelineReadModelSnapshot? snapshot)
            ? OperationTimelineReadModelResult.Available(snapshot)
            : OperationTimelineReadModelResult.NotFound(AuditFreshness.SafeUnavailable(_clock.UtcNow, "operation_timeline_projection_missing")));
    }

    private readonly record struct ScopedKey(string ManagedTenantId, string FolderId);
}
