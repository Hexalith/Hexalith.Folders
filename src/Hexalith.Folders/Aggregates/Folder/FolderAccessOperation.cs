namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderAccessOperation(
    FolderAccessOperationIntent Intent,
    FolderAccessPrincipalKind PrincipalKind,
    string PrincipalId,
    string Action);
