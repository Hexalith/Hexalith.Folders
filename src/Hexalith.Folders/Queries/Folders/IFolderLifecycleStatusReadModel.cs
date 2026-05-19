namespace Hexalith.Folders.Queries.Folders;

public interface IFolderLifecycleStatusReadModel
{
    Task<FolderLifecycleStatusReadModelResult> GetAsync(
        FolderLifecycleStatusReadModelRequest request,
        CancellationToken cancellationToken = default);
}
