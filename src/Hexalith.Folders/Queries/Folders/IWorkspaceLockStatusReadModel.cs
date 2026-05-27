namespace Hexalith.Folders.Queries.Folders;

public interface IWorkspaceLockStatusReadModel
{
    Task<WorkspaceLockStatusReadModelResult> GetAsync(
        WorkspaceLockStatusReadModelRequest request,
        CancellationToken cancellationToken = default);
}
