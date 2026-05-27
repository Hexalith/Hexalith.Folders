namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspaceFileDeleteOperationStoreResult(bool Accepted)
{
    public static WorkspaceFileDeleteOperationStoreResult Succeeded { get; } = new(true);

    public static WorkspaceFileDeleteOperationStoreResult Failed { get; } = new(false);
}
