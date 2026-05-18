namespace Hexalith.Folders.Aggregates.Folder;

public interface IFolderRepository
{
    FolderStreamName CreateStreamName(string managedTenantId, string folderId);

    FolderState Load(FolderStreamName streamName);

    FolderAppendOutcome AppendIfFingerprintAbsent(
        FolderStreamName streamName,
        string idempotencyKey,
        string fingerprint,
        IReadOnlyList<IFolderEvent> events);

    FolderIdempotencyLookupResult TryGetIdempotencyFingerprint(
        string managedTenantId,
        string folderId,
        string idempotencyKey,
        out string? fingerprint);
}
