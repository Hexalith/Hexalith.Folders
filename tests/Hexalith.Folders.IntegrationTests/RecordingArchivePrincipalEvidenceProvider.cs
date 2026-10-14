using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.IntegrationTests;

internal sealed class RecordingArchivePrincipalEvidenceProvider : IFolderPermissionEvidenceProvider
{
    public List<FolderPermissionEvidenceRequest> Requests { get; } = [];

    public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
        FolderPermissionEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(FolderPermissionEvidenceResult.Allowed("folder-a:7", organizationId: "org-a"));
    }
}
