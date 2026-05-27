namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspaceFileContentStoreResult(bool Accepted)
{
    public static WorkspaceFileContentStoreResult Succeeded { get; } = new(true);

    public static WorkspaceFileContentStoreResult Failed { get; } = new(false);
}

