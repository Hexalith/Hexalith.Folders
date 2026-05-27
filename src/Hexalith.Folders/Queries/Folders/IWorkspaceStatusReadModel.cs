namespace Hexalith.Folders.Queries.Folders;

public interface IWorkspaceStatusReadModel
{
    Task<WorkspaceStatusReadModelResult> GetAsync(
        WorkspaceStatusReadModelRequest request,
        CancellationToken cancellationToken = default);
}
