namespace Hexalith.Folders.Queries.Folders;

public interface IWorkspaceCleanupStatusReadModel
{
    Task<WorkspaceCleanupStatusReadModelResult> GetAsync(
        WorkspaceCleanupStatusReadModelRequest request,
        CancellationToken cancellationToken = default);
}
