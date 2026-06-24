namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// The Memories-free egress port the context-search handler uses to query the search index. Production binds the
/// Server-side Memories gateway; the fail-safe default is <see cref="UnavailableFolderSearchSource"/>. Keeping the
/// port and its DTOs Memories-free preserves the rule that core/Client/CLI/MCP/UI take no Memories dependency.
/// </summary>
public interface IFolderSearchSource
{
    /// <summary>Executes a security-scoped search and returns Memories-free hits (identity recovered, content dropped).</summary>
    Task<FolderSearchSourceResult> SearchAsync(
        FolderSearchSourceRequest request,
        CancellationToken cancellationToken = default);
}
