namespace Hexalith.Folders.Authorization;

public sealed record EffectivePermissionsReadModelResult(
    EffectivePermissionsReadModelStatus Status,
    EffectivePermissionsReadModelSnapshot? Snapshot,
    EffectivePermissionsFreshness Freshness)
{
    public static EffectivePermissionsReadModelResult Available(EffectivePermissionsReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new(EffectivePermissionsReadModelStatus.Available, snapshot, snapshot.Freshness);
    }

    public static EffectivePermissionsReadModelResult Stale(EffectivePermissionsReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new(EffectivePermissionsReadModelStatus.Stale, snapshot, snapshot.Freshness with { Stale = true });
    }

    public static EffectivePermissionsReadModelResult NotFound(EffectivePermissionsFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(freshness);

        return new(EffectivePermissionsReadModelStatus.NotFound, null, freshness);
    }

    public static EffectivePermissionsReadModelResult Malformed(EffectivePermissionsFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(freshness);

        return new(EffectivePermissionsReadModelStatus.Malformed, null, freshness with { Stale = true });
    }

    public static EffectivePermissionsReadModelResult Unavailable(string reasonCode, DateTimeOffset observedAt)
        => new(
            EffectivePermissionsReadModelStatus.Unavailable,
            null,
            EffectivePermissionsFreshness.SafeUnavailable(observedAt, reasonCode));
}
