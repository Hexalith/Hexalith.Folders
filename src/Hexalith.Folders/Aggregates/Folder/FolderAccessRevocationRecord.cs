namespace Hexalith.Folders.Aggregates.Folder;

// Captures a single revocation against an ACL tuple for C7 freshness audits and
// authorization replay. Stored in FolderAccessOverride.RevocationHistory so that a
// later grant for the same tuple cannot erase the metadata of a prior revoke.
public sealed record FolderAccessRevocationRecord(
    long AccessSequence,
    DateTimeOffset OccurredAt,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey);
