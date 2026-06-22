using System.Collections.Concurrent;

namespace Hexalith.Folders.Queries.Folders;

/// <summary>
/// In-memory <see cref="IWorkspaceTransitionEvidenceReadModel"/> for dev/test hosts. Seeded directly via
/// <see cref="Save"/>; production hosts replace it with a projection-backed read model.
/// </summary>
public sealed class InMemoryWorkspaceTransitionEvidenceReadModel : IWorkspaceTransitionEvidenceReadModel
{
    private readonly ConcurrentDictionary<string, WorkspaceTransitionEvidenceSnapshot> _snapshots = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task<WorkspaceTransitionEvidenceSnapshot?> GetAsync(
        WorkspaceTransitionEvidenceReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        _snapshots.TryGetValue(Key(request.ManagedTenantId, request.FolderId, request.WorkspaceId), out WorkspaceTransitionEvidenceSnapshot? snapshot);
        return Task.FromResult(snapshot);
    }

    /// <summary>
    /// Seeds or replaces a transition-evidence snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot.</param>
    public void Save(WorkspaceTransitionEvidenceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshots[Key(snapshot.ManagedTenantId, snapshot.FolderId, snapshot.WorkspaceId)] = snapshot;
    }

    private static string Key(string managedTenantId, string folderId, string workspaceId)
        => $"{managedTenantId}|{folderId}|{workspaceId}";
}
