using System.Collections.Concurrent;

using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Queries.Folders;

public sealed class InMemoryBranchRefPolicyReadModel(IUtcClock clock) : IBranchRefPolicyReadModel
{
    private readonly ConcurrentDictionary<ScopedSnapshotKey, BranchRefPolicyReadModelSnapshot> _snapshots = new();
    private readonly IUtcClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public void Save(BranchRefPolicyReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.FolderId);

        _snapshots[new ScopedSnapshotKey(snapshot.ManagedTenantId, snapshot.FolderId)] = snapshot;
    }

    public Task<BranchRefPolicyReadModelResult> GetAsync(
        BranchRefPolicyReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ManagedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FolderId);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_snapshots.TryGetValue(new ScopedSnapshotKey(request.ManagedTenantId, request.FolderId), out BranchRefPolicyReadModelSnapshot? snapshot)
            ? BranchRefPolicyReadModelResult.Available(ScopeForRequest(snapshot, request))
            : BranchRefPolicyReadModelResult.NotFound(
                FolderLifecycleFreshness.SafeUnavailable(_clock.UtcNow, "branch_ref_policy_projection_missing")));
    }

    private static BranchRefPolicyReadModelSnapshot ScopeForRequest(
        BranchRefPolicyReadModelSnapshot snapshot,
        BranchRefPolicyReadModelRequest request)
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

    private readonly record struct ScopedSnapshotKey(string ManagedTenantId, string FolderId);
}
