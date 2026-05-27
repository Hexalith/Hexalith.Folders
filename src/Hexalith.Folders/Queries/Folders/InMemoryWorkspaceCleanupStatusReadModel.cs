using System.Collections.Concurrent;

using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Queries.Folders;

public sealed class InMemoryWorkspaceCleanupStatusReadModel(IUtcClock clock) : IWorkspaceCleanupStatusReadModel
{
    private readonly ConcurrentDictionary<ScopedSnapshotKey, WorkspaceCleanupStatusReadModelSnapshot> _snapshots = new();
    private readonly IUtcClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public void Save(WorkspaceCleanupStatusReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.FolderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.WorkspaceId);

        _snapshots[new ScopedSnapshotKey(
            snapshot.ManagedTenantId,
            snapshot.FolderId,
            snapshot.WorkspaceId,
            snapshot.TaskId,
            snapshot.CorrelationId)] = snapshot;
    }

    public Task<WorkspaceCleanupStatusReadModelResult> GetAsync(
        WorkspaceCleanupStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FolderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkspaceId);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_snapshots.TryGetValue(
            new ScopedSnapshotKey(
                request.ManagedTenantId,
                request.FolderId,
                request.WorkspaceId,
                request.TaskId,
                request.CorrelationId),
            out WorkspaceCleanupStatusReadModelSnapshot? snapshot)
            ? WorkspaceCleanupStatusReadModelResult.Available(ScopeForRequest(snapshot, request))
            : WorkspaceCleanupStatusReadModelResult.NotFound(
                new FolderLifecycleFreshness(
                    "read_your_writes",
                    _clock.UtcNow,
                    null,
                    Stale: true,
                    "cleanup_status_projection_missing")));
    }

    private static WorkspaceCleanupStatusReadModelSnapshot ScopeForRequest(
        WorkspaceCleanupStatusReadModelSnapshot snapshot,
        WorkspaceCleanupStatusReadModelRequest request)
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

    private readonly record struct ScopedSnapshotKey(
        string ManagedTenantId,
        string FolderId,
        string WorkspaceId,
        string? TaskId,
        string? CorrelationId);
}
