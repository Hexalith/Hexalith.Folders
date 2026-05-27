namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceCleanupStatusReadModelResult(
    WorkspaceCleanupStatusReadModelStatus Status,
    WorkspaceCleanupStatusReadModelSnapshot? Snapshot,
    FolderLifecycleFreshness Freshness)
{
    public static WorkspaceCleanupStatusReadModelResult Available(WorkspaceCleanupStatusReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(WorkspaceCleanupStatusReadModelStatus.Available, snapshot, snapshot.Freshness);
    }

    public static WorkspaceCleanupStatusReadModelResult NotFound(FolderLifecycleFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(freshness);
        return new(WorkspaceCleanupStatusReadModelStatus.NotFound, null, freshness);
    }

    public static WorkspaceCleanupStatusReadModelResult Unavailable(string reasonCode, DateTimeOffset observedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        return new(
            WorkspaceCleanupStatusReadModelStatus.Unavailable,
            null,
            new FolderLifecycleFreshness("read_your_writes", observedAt, null, Stale: true, reasonCode));
    }
}
