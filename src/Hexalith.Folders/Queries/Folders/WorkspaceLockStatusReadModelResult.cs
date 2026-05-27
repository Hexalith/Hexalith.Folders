namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceLockStatusReadModelResult(
    WorkspaceLockStatusReadModelStatus Status,
    WorkspaceLockStatusReadModelSnapshot? Snapshot,
    FolderLifecycleFreshness Freshness)
{
    public static WorkspaceLockStatusReadModelResult Available(WorkspaceLockStatusReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(WorkspaceLockStatusReadModelStatus.Available, snapshot, snapshot.Freshness);
    }

    public static WorkspaceLockStatusReadModelResult NotFound(FolderLifecycleFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(freshness);
        return new(WorkspaceLockStatusReadModelStatus.NotFound, null, freshness);
    }

    public static WorkspaceLockStatusReadModelResult Unavailable(string reasonCode, DateTimeOffset observedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        return new(
            WorkspaceLockStatusReadModelStatus.Unavailable,
            null,
            FolderLifecycleFreshness.SafeUnavailable(observedAt, reasonCode));
    }
}
