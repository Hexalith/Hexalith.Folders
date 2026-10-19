using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Tests;

internal sealed class OperationalStatusPermissionEvidence(FolderPermissionEvidenceStatus status) : IFolderPermissionEvidenceProvider
{
    public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(FolderPermissionEvidenceRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(FolderPermissionEvidenceResult.FromStatus(status, "authority-a"));
}
