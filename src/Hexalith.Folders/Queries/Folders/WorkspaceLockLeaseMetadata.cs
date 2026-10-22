namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceLockLeaseMetadata(
    string LockId,
    string LeaseStatus,
    DateTimeOffset AcquiredAt,
    DateTimeOffset EffectiveAt,
    DateTimeOffset ExpiresAt,
    string? HolderRef)
{
    /// <summary>Gets the monotonic lock acquisition fence for subsequent file mutations.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? LockFence { get; init; }
}
