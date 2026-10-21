using Hexalith.Folders.Authorization;
namespace Hexalith.Folders.Server.Tests;

/// <summary>Synthetic independent native authority used only to exercise contract behavior, never production coverage.</summary>
internal sealed class FolderFilePermissionEvidenceProvider : IFolderPermissionEvidenceProvider
{
    internal Func<int, FolderPermissionEvidenceResult>? Evidence { get; set; }
    internal int Checks { get; private set; }
    public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(FolderPermissionEvidenceRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Checks++;
        if (request.ManagedTenantId != "tenant-a" || request.OperationScope != "folder-a" || request.PrincipalId != "actor")
            return Task.FromResult(FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.NotFoundSafe, null));
        return Task.FromResult(Evidence?.Invoke(Checks) ?? Current());
    }
    internal static FolderPermissionEvidenceResult Current() => FolderPermissionEvidenceResult.Allowed("5", organizationId: "tenant-a") with
    {
        ResourceVersion = "5",
        PermissionVersion = "[5,1]",
        NativeTenantRevision = "synthetic-native-tenant-7",
        LastVerifiedAt = FolderFileOwnerReadFixture.Now,
    };
}
