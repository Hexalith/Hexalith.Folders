using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Tests.Authorization;

internal sealed class RevokingFolderPermissionEvidenceProvider : IFolderPermissionEvidenceProvider
{
    public List<FolderPermissionEvidenceRequest> Requests { get; } = [];

    public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
        FolderPermissionEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        FolderPermissionEvidenceResult result = Requests.Count == 1
            ? FolderPermissionEvidenceResult.Allowed("folder-a:7", organizationId: "organization-a")
            : FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Denied, "folder-a:8");
        return Task.FromResult(result);
    }
}
