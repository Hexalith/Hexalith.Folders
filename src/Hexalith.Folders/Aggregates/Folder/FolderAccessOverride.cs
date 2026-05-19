namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderAccessOverride(
    FolderAccessEntryKey Key,
    bool IsGranted,
    long AccessSequence,
    string OperationIntent,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey);
