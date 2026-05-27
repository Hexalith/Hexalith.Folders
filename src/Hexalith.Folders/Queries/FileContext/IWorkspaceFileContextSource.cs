namespace Hexalith.Folders.Queries.FileContext;

public interface IWorkspaceFileContextSource
{
    Task<WorkspaceFileContextSourceResult> QueryAsync(
        WorkspaceFileContextSourceRequest request,
        CancellationToken cancellationToken = default);
}
