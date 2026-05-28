using System.Collections.Concurrent;

using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Queries.Audit;

public sealed class InMemoryAuditRecordReadModel(IUtcClock clock) : IAuditRecordReadModel
{
    private readonly ConcurrentDictionary<ScopedKey, AuditRecordReadModelSnapshot> _snapshots = new();
    private readonly IUtcClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private int _getCount;

    public int GetCount => _getCount;

    public void Save(AuditRecordReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.FolderId);
        _snapshots[new ScopedKey(snapshot.ManagedTenantId, snapshot.FolderId, snapshot.Record.AuditRecordId)] = snapshot;
    }

    public Task<AuditRecordReadModelResult> GetAsync(
        AuditRecordReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FolderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AuditRecordId);
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _getCount);

        return Task.FromResult(_snapshots.TryGetValue(new ScopedKey(request.ManagedTenantId, request.FolderId, request.AuditRecordId), out AuditRecordReadModelSnapshot? snapshot)
            ? AuditRecordReadModelResult.Available(snapshot)
            : AuditRecordReadModelResult.NotFound(AuditFreshness.SafeUnavailable(_clock.UtcNow, "audit_record_projection_missing")));
    }

    private readonly record struct ScopedKey(string ManagedTenantId, string FolderId, string AuditRecordId);
}
