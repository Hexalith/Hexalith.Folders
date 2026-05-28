namespace Hexalith.Folders.Queries.Audit;

public sealed record OperationTimelineReadModelResult(
    AuditReadModelStatus Status,
    OperationTimelineReadModelSnapshot? Snapshot,
    AuditFreshness Freshness)
{
    public static OperationTimelineReadModelResult Available(OperationTimelineReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(AuditReadModelStatus.Available, snapshot, snapshot.Freshness);
    }

    public static OperationTimelineReadModelResult Stale(OperationTimelineReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(AuditReadModelStatus.Stale, snapshot, snapshot.Freshness with { Stale = true });
    }

    public static OperationTimelineReadModelResult NotFound(AuditFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(freshness);
        return new(AuditReadModelStatus.NotFound, null, freshness);
    }

    public static OperationTimelineReadModelResult Unavailable(string reasonCode, DateTimeOffset observedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        return new(AuditReadModelStatus.Unavailable, null, AuditFreshness.SafeUnavailable(observedAt, reasonCode));
    }
}
