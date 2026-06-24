namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// A Memories-free search hit with identity recovered from the index entry's source URI (never from the content
/// snippet, which is dropped). The handler re-checks these recovered components against the authorized scope (the
/// index is security-untrusted) before any hit can cross the boundary.
/// </summary>
/// <param name="ManagedTenantId">The recovered managed tenant id.</param>
/// <param name="OrganizationId">The recovered organization id.</param>
/// <param name="FolderId">The recovered folder id.</param>
/// <param name="WorkspaceId">The recovered workspace id.</param>
/// <param name="FileVersionId">The recovered opaque file-version id.</param>
/// <param name="Score">The relevance score from the search axis.</param>
public sealed record FolderSearchSourceHit(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string WorkspaceId,
    string FileVersionId,
    double Score);
