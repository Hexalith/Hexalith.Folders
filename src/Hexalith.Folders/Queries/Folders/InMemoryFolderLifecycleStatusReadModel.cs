using System.Collections.Concurrent;

using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Queries.Folders;

public sealed class InMemoryFolderLifecycleStatusReadModel(IUtcClock clock) : IFolderLifecycleStatusReadModel
{
    private readonly ConcurrentDictionary<ScopedSnapshotKey, FolderLifecycleStatusReadModelSnapshot> _snapshots = new();
    private readonly IUtcClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public void Save(FolderLifecycleStatusReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.FolderId);

        _snapshots[new ScopedSnapshotKey(snapshot.ManagedTenantId, snapshot.FolderId)] = snapshot;
    }

    public Task<FolderLifecycleStatusReadModelResult> GetAsync(
        FolderLifecycleStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FolderId);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_snapshots.TryGetValue(new ScopedSnapshotKey(request.ManagedTenantId, request.FolderId), out FolderLifecycleStatusReadModelSnapshot? snapshot)
            ? FolderLifecycleStatusReadModelResult.Available(snapshot)
            : FolderLifecycleStatusReadModelResult.NotFound(
                FolderLifecycleFreshness.SafeUnavailable(_clock.UtcNow, "folder_lifecycle_projection_missing")));
    }

    private readonly record struct ScopedSnapshotKey(string ManagedTenantId, string FolderId);
}
