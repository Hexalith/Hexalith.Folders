namespace Hexalith.Folders.Aggregates.Folder;

public sealed class UnavailableWorkspaceFileDeleteOperationStore : IWorkspaceFileDeleteOperationStore
{
    public Task<WorkspaceFileDeleteOperationStoreResult> StageAsync(
        WorkspaceFileDeleteOperationStoreRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(WorkspaceFileDeleteOperationStoreResult.Failed);
    }
}
