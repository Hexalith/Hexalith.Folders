namespace Hexalith.Folders.Queries.Audit;

public sealed record AuditRecordReadModelResult(
    AuditReadModelStatus Status,
    AuditRecordReadModelSnapshot? Snapshot,
    AuditFreshness Freshness)
{
    public static AuditRecordReadModelResult Available(AuditRecordReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(AuditReadModelStatus.Available, snapshot, snapshot.Freshness);
    }

    public static AuditRecordReadModelResult Stale(AuditRecordReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(AuditReadModelStatus.Stale, snapshot, snapshot.Freshness with { Stale = true });
    }

    public static AuditRecordReadModelResult NotFound(AuditFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(freshness);
        return new(AuditReadModelStatus.NotFound, null, freshness);
    }

    public static AuditRecordReadModelResult Unavailable(string reasonCode, DateTimeOffset observedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        return new(AuditReadModelStatus.Unavailable, null, AuditFreshness.SafeUnavailable(observedAt, reasonCode));
    }
}
