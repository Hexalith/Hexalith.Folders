using System.Collections.Concurrent;

namespace Hexalith.Folders.Authorization;

public sealed class InMemoryEffectivePermissionsReadModel : IEffectivePermissionsReadModel
{
    private readonly ConcurrentDictionary<ScopedSnapshotKey, EffectivePermissionsReadModelSnapshot> _snapshots = new();

    public void Save(EffectivePermissionsReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _snapshots[new ScopedSnapshotKey(snapshot.ManagedTenantId, snapshot.FolderId)] = snapshot;
    }

    public Task<EffectivePermissionsReadModelResult> GetAsync(
        EffectivePermissionsReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_snapshots.TryGetValue(new ScopedSnapshotKey(request.ManagedTenantId, request.FolderId), out EffectivePermissionsReadModelSnapshot? snapshot)
            ? EffectivePermissionsReadModelResult.Available(snapshot)
            : EffectivePermissionsReadModelResult.NotFound(
                EffectivePermissionsFreshness.SafeUnavailable(DateTimeOffset.UnixEpoch, "folder_projection_missing")));
    }

    private readonly record struct ScopedSnapshotKey(string ManagedTenantId, string FolderId);
}
