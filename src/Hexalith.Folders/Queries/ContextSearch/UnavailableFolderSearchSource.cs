namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// Fail-safe default <see cref="IFolderSearchSource"/>: reports the search index unavailable so the facade never
/// silently returns unverified results when no live Memories gateway is wired. Production overrides this with the
/// Server-side <c>MemoriesFolderSearchSource</c> (mirrors the <c>UnavailableWorkspaceFileContextSource</c> default).
/// </summary>
public sealed class UnavailableFolderSearchSource : IFolderSearchSource
{
    public Task<FolderSearchSourceResult> SearchAsync(
        FolderSearchSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(FolderSearchSourceResult.Unavailable());
    }
}
