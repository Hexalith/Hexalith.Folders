namespace Hexalith.Folders.Authorization;

public sealed record EffectivePermissionsFreshness(
    string ReadConsistency,
    DateTimeOffset ObservedAt,
    string? ProjectionWatermark,
    bool Stale,
    string? ReasonCode)
{
    public static EffectivePermissionsFreshness SafeUnavailable(DateTimeOffset observedAt, string reasonCode)
        => new("read_your_writes", observedAt, null, Stale: true, reasonCode);
}
