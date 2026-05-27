namespace Hexalith.Folders.Aggregates.Folder;

public interface IWorkspaceFileContentStore
{
    Task<WorkspaceFileContentStoreResult> StageAsync(
        WorkspaceFileContentStoreRequest request,
        CancellationToken cancellationToken = default);
}

