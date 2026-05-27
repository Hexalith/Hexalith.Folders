namespace Hexalith.Folders.Aggregates.Folder;

public interface IWorkspaceCommitExecutor
{
    Task<WorkspaceCommitExecutionResult> CommitAsync(
        WorkspaceCommitExecutionRequest request,
        CancellationToken cancellationToken = default);
}
