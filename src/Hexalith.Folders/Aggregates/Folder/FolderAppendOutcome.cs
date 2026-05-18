namespace Hexalith.Folders.Aggregates.Folder;

public enum FolderAppendOutcome
{
    Appended,
    FingerprintMatched,
    FingerprintConflict,
    AppendConflict,
}
