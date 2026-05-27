namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceStatusReadModelResult(
    WorkspaceStatusReadModelStatus Status,
    WorkspaceStatusReadModelSnapshot? Snapshot,
    FolderLifecycleFreshness Freshness)
{
    public static WorkspaceStatusReadModelResult Available(WorkspaceStatusReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(WorkspaceStatusReadModelStatus.Available, snapshot, snapshot.Freshness);
    }

    public static WorkspaceStatusReadModelResult NotFound(FolderLifecycleFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(freshness);
        return new(WorkspaceStatusReadModelStatus.NotFound, null, freshness);
    }

    public static WorkspaceStatusReadModelResult Unavailable(string reasonCode, DateTimeOffset observedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        return new(
            WorkspaceStatusReadModelStatus.Unavailable,
            null,
            new FolderLifecycleFreshness("read_your_writes", observedAt, null, Stale: true, reasonCode));
    }
}
