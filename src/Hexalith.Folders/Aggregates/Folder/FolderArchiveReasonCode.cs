namespace Hexalith.Folders.Aggregates.Folder;

public enum FolderArchiveReasonCode
{
    // Explicit sentinel so a TryParse miss does not silently coerce unknown input to a
    // valid reason. The zero member is "no reason supplied"; supported reasons start at 1.
    None = 0,
    CallerRequested = 1,
    PolicyRetention = 2,
    OperatorReview = 3,
}
