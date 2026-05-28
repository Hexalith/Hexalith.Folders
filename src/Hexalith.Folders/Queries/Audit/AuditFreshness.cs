namespace Hexalith.Folders.Queries.Audit;

public sealed record AuditFreshness(
    string ReadConsistency,
    DateTimeOffset ObservedAt,
    string? ProjectionWatermark,
    bool Stale,
    string? ReasonCode)
{
    public static AuditFreshness SafeUnavailable(DateTimeOffset observedAt, string reasonCode)
        => new("eventually_consistent", observedAt, null, Stale: true, reasonCode);
}
