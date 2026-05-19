namespace Hexalith.Folders.Authorization;

public interface IFolderPermissionEvidenceProvider
{
    Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
        FolderPermissionEvidenceRequest request,
        CancellationToken cancellationToken = default);
}
