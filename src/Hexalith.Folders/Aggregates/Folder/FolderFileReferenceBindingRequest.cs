namespace Hexalith.Folders.Aggregates.Folder;

/// <summary>Exact candidate mapping requiring independent current Projects-owned reference authority.</summary>
/// <param name="TenantId">The authenticated same Tenant.</param>
/// <param name="FolderId">The native owning parent.</param>
/// <param name="OwnerFileId">The owner-allocated exact File incarnation.</param>
/// <param name="ProjectFileReferenceId">The separately owned Projects reference.</param>
/// <param name="ExpectedNativeMutationId">The existing native mutation identity for late binding; null for a newly allocated incarnation.</param>
public sealed record FolderFileReferenceBindingRequest(string TenantId, string FolderId, string OwnerFileId,
    string ProjectFileReferenceId, string? ExpectedNativeMutationId);
