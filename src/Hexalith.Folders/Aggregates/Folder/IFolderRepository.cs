namespace Hexalith.Folders.Aggregates.Folder;

public interface IFolderRepository
{
    FolderStreamName CreateStreamName(string managedTenantId, string folderId);

    FolderState Load(FolderStreamName streamName);

    /// <summary>Loads state while observing cancellation at the read boundary.</summary>
    FolderState Load(FolderStreamName streamName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FolderState state = Load(streamName);
        cancellationToken.ThrowIfCancellationRequested();
        return state;
    }

    FolderAppendOutcome AppendIfFingerprintAbsent(
        FolderStreamName streamName,
        string idempotencyKey,
        string fingerprint,
        IReadOnlyList<IFolderEvent> events);

    /// <summary>Checks cancellation before starting the atomic append.</summary>
    FolderAppendOutcome AppendIfFingerprintAbsent(
        FolderStreamName streamName,
        string idempotencyKey,
        string fingerprint,
        IReadOnlyList<IFolderEvent> events,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return AppendIfFingerprintAbsent(streamName, idempotencyKey, fingerprint, events);
    }

    // The ledger key is `(streamName, idempotencyKey)`. Both lookup and append address
    // the ledger by `FolderStreamName` so any production implementation must store and
    // retrieve under the same key; there is no longer a `(tenantId, folderId)` overload
    // that an implementation could resolve to a different key than the append path.
    FolderIdempotencyLookupResult TryGetIdempotencyFingerprint(
        FolderStreamName streamName,
        string idempotencyKey,
        out string? fingerprint);
}
