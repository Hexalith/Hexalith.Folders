namespace Hexalith.Folders.Queries.Audit;

public sealed record AuditTrailReadModelResult(
    AuditReadModelStatus Status,
    AuditTrailReadModelSnapshot? Snapshot,
    AuditFreshness Freshness)
{
    public static AuditTrailReadModelResult Available(AuditTrailReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(AuditReadModelStatus.Available, snapshot, snapshot.Freshness);
    }

    public static AuditTrailReadModelResult Stale(AuditTrailReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(AuditReadModelStatus.Stale, snapshot, snapshot.Freshness with { Stale = true });
    }

    public static AuditTrailReadModelResult NotFound(AuditFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(freshness);
        return new(AuditReadModelStatus.NotFound, null, freshness);
    }

    public static AuditTrailReadModelResult Unavailable(string reasonCode, DateTimeOffset observedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        return new(AuditReadModelStatus.Unavailable, null, AuditFreshness.SafeUnavailable(observedAt, reasonCode));
    }
}
