namespace Hexalith.Folders.Authorization;

/// <summary>Reads a folder's durable ACL after caller authorization.</summary>
public interface IFolderAclReadModel
{
    /// <summary>Gets a current folder permission projection, or null when no folder is projected.</summary>
    Task<FolderPermissionProjection?> GetCurrentAsync(string tenantId, string folderId,
        CancellationToken cancellationToken = default);
}
