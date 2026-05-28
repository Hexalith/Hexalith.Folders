using System.Collections.Concurrent;

using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Queries.Audit;

public sealed class InMemoryAuditTrailReadModel(IUtcClock clock) : IAuditTrailReadModel
{
    private readonly ConcurrentDictionary<ScopedKey, AuditTrailReadModelSnapshot> _snapshots = new();
    private readonly IUtcClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private int _getCount;

    public int GetCount => _getCount;

    public void Save(AuditTrailReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.FolderId);
        _snapshots[new ScopedKey(snapshot.ManagedTenantId, snapshot.FolderId)] = snapshot;
    }

    public Task<AuditTrailReadModelResult> GetAsync(
        AuditTrailReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FolderId);
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _getCount);

        return Task.FromResult(_snapshots.TryGetValue(new ScopedKey(request.ManagedTenantId, request.FolderId), out AuditTrailReadModelSnapshot? snapshot)
            ? AuditTrailReadModelResult.Available(snapshot)
            : AuditTrailReadModelResult.NotFound(AuditFreshness.SafeUnavailable(_clock.UtcNow, "audit_trail_projection_missing")));
    }

    private readonly record struct ScopedKey(string ManagedTenantId, string FolderId);
}
