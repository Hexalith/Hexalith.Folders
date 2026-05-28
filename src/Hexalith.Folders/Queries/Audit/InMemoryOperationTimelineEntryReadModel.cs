using System.Collections.Concurrent;

using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Queries.Audit;

public sealed class InMemoryOperationTimelineEntryReadModel(IUtcClock clock) : IOperationTimelineEntryReadModel
{
    private readonly ConcurrentDictionary<ScopedKey, OperationTimelineEntryReadModelSnapshot> _snapshots = new();
    private readonly IUtcClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private int _getCount;

    public int GetCount => _getCount;

    public void Save(OperationTimelineEntryReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.FolderId);
        _snapshots[new ScopedKey(snapshot.ManagedTenantId, snapshot.FolderId, snapshot.Entry.TimelineEntryId)] = snapshot;
    }

    public Task<OperationTimelineEntryReadModelResult> GetAsync(
        OperationTimelineEntryReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FolderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TimelineEntryId);
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _getCount);

        return Task.FromResult(_snapshots.TryGetValue(new ScopedKey(request.ManagedTenantId, request.FolderId, request.TimelineEntryId), out OperationTimelineEntryReadModelSnapshot? snapshot)
            ? OperationTimelineEntryReadModelResult.Available(snapshot)
            : OperationTimelineEntryReadModelResult.NotFound(AuditFreshness.SafeUnavailable(_clock.UtcNow, "operation_timeline_entry_projection_missing")));
    }

    private readonly record struct ScopedKey(string ManagedTenantId, string FolderId, string TimelineEntryId);
}
