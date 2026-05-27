using System.Collections.Concurrent;

using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Queries.Folders;

public sealed class InMemoryWorkspaceStatusReadModel(IUtcClock clock) : IWorkspaceStatusReadModel
{
    private readonly ConcurrentDictionary<ScopedSnapshotKey, WorkspaceStatusReadModelSnapshot> _snapshots = new();
    private readonly IUtcClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public void Save(WorkspaceStatusReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.FolderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.WorkspaceId);

        _snapshots[new ScopedSnapshotKey(snapshot.ManagedTenantId, snapshot.FolderId, snapshot.WorkspaceId)] = snapshot;
    }

    public Task<WorkspaceStatusReadModelResult> GetAsync(
        WorkspaceStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FolderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkspaceId);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_snapshots.TryGetValue(
            new ScopedSnapshotKey(request.ManagedTenantId, request.FolderId, request.WorkspaceId),
            out WorkspaceStatusReadModelSnapshot? snapshot)
            ? WorkspaceStatusReadModelResult.Available(ScopeForRequest(snapshot, request))
            : WorkspaceStatusReadModelResult.NotFound(
                new FolderLifecycleFreshness(
                    "read_your_writes",
                    _clock.UtcNow,
                    null,
                    Stale: true,
                    "workspace_status_projection_missing")));
    }

    private static WorkspaceStatusReadModelSnapshot ScopeForRequest(
        WorkspaceStatusReadModelSnapshot snapshot,
        WorkspaceStatusReadModelRequest request)
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
