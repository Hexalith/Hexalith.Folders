namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceLockLeaseMetadata(
    string LockId,
    string LeaseStatus,
    DateTimeOffset AcquiredAt,
    DateTimeOffset EffectiveAt,
    DateTimeOffset ExpiresAt,
    string? HolderRef);
