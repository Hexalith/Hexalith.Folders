using System.Collections.Concurrent;

using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Queries.Folders;

public sealed class InMemoryWorkspaceLockStatusReadModel(IUtcClock clock) : IWorkspaceLockStatusReadModel
{
    private readonly ConcurrentDictionary<ScopedSnapshotKey, WorkspaceLockStatusReadModelSnapshot> _snapshots = new();
    private readonly IUtcClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public void Save(WorkspaceLockStatusReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.FolderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.WorkspaceId);

        _snapshots[new ScopedSnapshotKey(snapshot.ManagedTenantId, snapshot.FolderId, snapshot.WorkspaceId)] = snapshot;
    }

    public Task<WorkspaceLockStatusReadModelResult> GetAsync(
        WorkspaceLockStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FolderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkspaceId);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_snapshots.TryGetValue(
            new ScopedSnapshotKey(request.ManagedTenantId, request.FolderId, request.WorkspaceId),
            out WorkspaceLockStatusReadModelSnapshot? snapshot)
            ? WorkspaceLockStatusReadModelResult.Available(ScopeForRequest(snapshot, request))
            : WorkspaceLockStatusReadModelResult.NotFound(
                FolderLifecycleFreshness.SafeUnavailable(_clock.UtcNow, "workspace_lock_projection_missing")));
    }

    private static WorkspaceLockStatusReadModelSnapshot ScopeForRequest(
        WorkspaceLockStatusReadModelSnapshot snapshot,
        WorkspaceLockStatusReadModelRequest request)
        => snapshot with
        {
            EvidenceScope = new FolderLifecycleEvidenceScope(
                request.ManagedTenantId,
                request.PrincipalId,
                request.ActionToken,
                request.TaskId,
                request.CorrelationId,
                request.AuthorizationWatermark),
        };

    private readonly record struct ScopedSnapshotKey(string ManagedTenantId, string FolderId, string WorkspaceId);
}
