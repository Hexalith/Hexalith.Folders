namespace Hexalith.Folders.Queries.Folders;

public sealed record TaskStatusReadModelResult(
    TaskStatusReadModelStatus Status,
    TaskStatusReadModelSnapshot? Snapshot,
    FolderLifecycleFreshness Freshness)
{
    public static TaskStatusReadModelResult Available(TaskStatusReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(TaskStatusReadModelStatus.Available, snapshot, snapshot.Freshness);
    }

    public static TaskStatusReadModelResult NotFound(FolderLifecycleFreshness freshness)
        => new(TaskStatusReadModelStatus.NotFound, Snapshot: null, freshness);

    public static TaskStatusReadModelResult Unavailable(string reasonCode, DateTimeOffset observedAt)
        => new(
            TaskStatusReadModelStatus.Unavailable,
            Snapshot: null,
            new FolderLifecycleFreshness("eventually_consistent", observedAt, null, Stale: true, reasonCode));
}
