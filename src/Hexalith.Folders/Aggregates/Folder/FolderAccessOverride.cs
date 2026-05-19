namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderAccessOverride(
    FolderAccessEntryKey Key,
    bool IsGranted,
    long AccessSequence,
    DateTimeOffset OccurredAt,
    string OperationIntent,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    IReadOnlyList<FolderAccessRevocationRecord> RevocationHistory);
