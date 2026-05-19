namespace Hexalith.Folders.Queries.Folders;

public sealed record FolderLifecycleStatusReadModelResult(
    FolderLifecycleStatusReadModelStatus Status,
    FolderLifecycleStatusReadModelSnapshot? Snapshot,
    FolderLifecycleFreshness Freshness)
{
    public static FolderLifecycleStatusReadModelResult Available(FolderLifecycleStatusReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new(FolderLifecycleStatusReadModelStatus.Available, snapshot, snapshot.Freshness);
    }

    public static FolderLifecycleStatusReadModelResult Stale(FolderLifecycleStatusReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new(FolderLifecycleStatusReadModelStatus.Stale, snapshot, snapshot.Freshness with { Stale = true });
    }

    public static FolderLifecycleStatusReadModelResult NotFound(FolderLifecycleFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(freshness);

        return new(FolderLifecycleStatusReadModelStatus.NotFound, null, freshness);
    }

    public static FolderLifecycleStatusReadModelResult Malformed(FolderLifecycleFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(freshness);

        return new(FolderLifecycleStatusReadModelStatus.Malformed, null, freshness with { Stale = true });
    }

    public static FolderLifecycleStatusReadModelResult Unavailable(string reasonCode, DateTimeOffset observedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);

        return new(
            FolderLifecycleStatusReadModelStatus.Unavailable,
            null,
            FolderLifecycleFreshness.SafeUnavailable(observedAt, reasonCode));
    }
}
