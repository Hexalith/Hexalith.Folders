namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderCommandValidationResult(
    bool IsAccepted,
    FolderResultCode Code,
    string? IdempotencyFingerprint,
    IReadOnlyList<string> CanonicalTags)
{
    public static FolderCommandValidationResult Accepted(string idempotencyFingerprint, IReadOnlyList<string> canonicalTags)
        => new(true, FolderResultCode.Created, idempotencyFingerprint, canonicalTags);

    public static FolderCommandValidationResult Rejected(FolderResultCode code)
        => new(false, code, null, []);
}
