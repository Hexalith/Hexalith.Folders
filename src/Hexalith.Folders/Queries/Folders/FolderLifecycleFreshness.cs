namespace Hexalith.Folders.Queries.Folders;

public sealed record FolderLifecycleFreshness(
    string ReadConsistency,
    DateTimeOffset ObservedAt,
    string? ProjectionWatermark,
    bool Stale,
    string? ReasonCode)
{
    public static FolderLifecycleFreshness SafeUnavailable(DateTimeOffset observedAt, string reasonCode)
        => new("eventually_consistent", observedAt, null, Stale: true, reasonCode);
}
