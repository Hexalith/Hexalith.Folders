namespace Hexalith.Folders.Queries.Folders;

public sealed record BranchRefPolicyReadModelResult(
    BranchRefPolicyReadModelStatus Status,
    BranchRefPolicyReadModelSnapshot? Snapshot,
    FolderLifecycleFreshness Freshness)
{
    public static BranchRefPolicyReadModelResult Available(BranchRefPolicyReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new(BranchRefPolicyReadModelStatus.Available, snapshot, snapshot.Freshness);
    }

    public static BranchRefPolicyReadModelResult NotFound(FolderLifecycleFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(freshness);

        return new(BranchRefPolicyReadModelStatus.NotFound, null, freshness);
    }

    public static BranchRefPolicyReadModelResult Unavailable(string reasonCode, DateTimeOffset observedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);

        return new(
            BranchRefPolicyReadModelStatus.Unavailable,
            null,
            FolderLifecycleFreshness.SafeUnavailable(observedAt, reasonCode));
    }
}
