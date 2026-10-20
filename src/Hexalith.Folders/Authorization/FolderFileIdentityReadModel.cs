namespace Hexalith.Folders.Authorization;

/// <summary>Isolated content-free identity facts at an exact Folder source position; this is not a complete Tenant inventory or a grant.</summary>
/// <param name="TenantId">The exact owner Tenant.</param>
/// <param name="FolderId">The exact parent Folder.</param>
/// <param name="LastSequence">The native Folder event position.</param>
/// <param name="ProjectedAt">The native projection observation instant.</param>
/// <param name="Files">The explicit owner identities, including invalidated incarnations.</param>
public sealed record FolderFileIdentityReadModel(string TenantId, string FolderId, long LastSequence,
    DateTimeOffset ProjectedAt, IReadOnlyList<FolderFileIdentityMetadata> Files)
{
    /// <summary>Builds a component-delimited key; no path or provider namespace is accepted.</summary>
    public static string Key(string tenantId, string folderId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        return $"projection:folders:file-identities:{tenantId.Length}:{tenantId}{folderId.Length}:{folderId}";
    }
}
