using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

internal sealed class FinalAclRevokingPermissions(int allowedCalls = 1) : IFolderPermissionEvidenceProvider
{
    public int Calls { get; private set; }

    public List<FolderPermissionEvidenceRequest> Requests { get; } = [];

    public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
        FolderPermissionEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        Requests.Add(request);
        return Task.FromResult(Calls > allowedCalls
            ? FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Denied, "organization-a:8")
            : FolderPermissionEvidenceResult.Allowed("organization-a:7", organizationId: "tenant-a"));
    }
}
