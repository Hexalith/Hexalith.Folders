namespace Hexalith.Folders.Aggregates.Folder;

/// <summary>Independently verifies current Projects ownership, explicit re-verification and whole-Tenant uniqueness of an exact File mapping.</summary>
/// <remarks>No default authority is registered. Generic Folder mutation permission, a supplied reference ID,
/// a path digest or a historic mutation cannot certify Projects reference authority.</remarks>
public interface IFolderFileReferenceBindingAuthority
{
    /// <summary>Authorizes the exact mapping against current native Projects authority and the expected owner incarnation.</summary>
    Task<bool> AuthorizeAsync(FolderFileReferenceBindingRequest request, CancellationToken cancellationToken = default);
}
