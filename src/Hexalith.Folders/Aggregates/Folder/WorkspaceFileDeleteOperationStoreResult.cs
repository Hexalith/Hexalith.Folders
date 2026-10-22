namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspaceFileDeleteOperationStoreResult(bool Accepted)
{
    /// <summary>Gets the metadata-only source binding after complete preparation.</summary>
    public WorkspaceContentBinding? Binding { get; init; }
    /// <summary>Gets the safe failure classification.</summary>
    public FolderResultCode FailureCode { get; init; } = FolderResultCode.FileOperationFailed;

    public static WorkspaceFileDeleteOperationStoreResult Succeeded { get; } = new(true);

    public static WorkspaceFileDeleteOperationStoreResult Failed { get; } = new(false);
}
