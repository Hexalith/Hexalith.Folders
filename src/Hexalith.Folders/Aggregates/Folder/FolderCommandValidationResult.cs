namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderCommandValidationResult(
    bool IsAccepted,
    FolderResultCode Code,
    string? IdempotencyFingerprint,
    IReadOnlyList<string> CanonicalTags,
    IReadOnlyList<FolderAccessOperation> AccessOperations)
{
    public static FolderCommandValidationResult Accepted(string idempotencyFingerprint, IReadOnlyList<string> canonicalTags)
        => new(true, FolderResultCode.Created, idempotencyFingerprint, canonicalTags, []);

    public static FolderCommandValidationResult AcceptedAccess(
        string idempotencyFingerprint,
        IReadOnlyList<FolderAccessOperation> accessOperations)
        => new(true, FolderResultCode.Accepted, idempotencyFingerprint, [], accessOperations);

    public static FolderCommandValidationResult Rejected(FolderResultCode code)
        => new(false, code, null, [], []);
}
