using System.Collections.Concurrent;

namespace Hexalith.Folders.Queries.Folders;

public sealed class InMemoryFolderLifecycleStatusReadModel : IFolderLifecycleStatusReadModel
{
    private readonly ConcurrentDictionary<ScopedSnapshotKey, FolderLifecycleStatusReadModelSnapshot> _snapshots = new();

    public void Save(FolderLifecycleStatusReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _snapshots[new ScopedSnapshotKey(snapshot.ManagedTenantId, snapshot.FolderId)] = snapshot;
    }

    public Task<FolderLifecycleStatusReadModelResult> GetAsync(
        FolderLifecycleStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_snapshots.TryGetValue(new ScopedSnapshotKey(request.ManagedTenantId, request.FolderId), out FolderLifecycleStatusReadModelSnapshot? snapshot)
            ? FolderLifecycleStatusReadModelResult.Available(snapshot)
            : FolderLifecycleStatusReadModelResult.NotFound(
                FolderLifecycleFreshness.SafeUnavailable(DateTimeOffset.UnixEpoch, "folder_lifecycle_projection_missing")));
    }

    private readonly record struct ScopedSnapshotKey(string ManagedTenantId, string FolderId);
}
