using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Tests;

internal sealed class ChangingOrganizationAclPermissionEvidenceProvider : IFolderPermissionEvidenceProvider
{
    internal int Reads { get; private set; }

    internal void Reset() => Reads = 0;

    public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(FolderPermissionEvidenceRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(++Reads == 1
            ? FolderPermissionEvidenceResult.Allowed("1", organizationId: "tenant-a")
            : FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Denied, "2"));
}
