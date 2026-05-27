namespace Hexalith.Folders.Aggregates.Folder;

public interface IWorkspaceFileDeleteOperationStore
{
    Task<WorkspaceFileDeleteOperationStoreResult> StageAsync(
        WorkspaceFileDeleteOperationStoreRequest request,
        CancellationToken cancellationToken = default);
}
