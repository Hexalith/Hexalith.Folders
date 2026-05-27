namespace Hexalith.Folders.Aggregates.Folder;

public sealed class UnavailableWorkspaceFileContentStore : IWorkspaceFileContentStore
{
    public Task<WorkspaceFileContentStoreResult> StageAsync(
        WorkspaceFileContentStoreRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(WorkspaceFileContentStoreResult.Failed);
    }
}

