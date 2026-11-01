using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Fails any protected authority observation in a request that the host must deny first.</summary>
internal sealed class DeniedTenantAccessProjectionStore : IFolderTenantAccessProjectionStore
{
    public int Calls { get; private set; }

    public Task<FolderTenantAccessProjection?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        Calls++;
        throw new InvalidOperationException("A denied credential observed tenant authority.");
    }

    public Task SaveAsync(FolderTenantAccessProjection projection, CancellationToken cancellationToken = default)
    {
        Calls++;
        throw new InvalidOperationException("A denied credential changed tenant authority.");
    }
}
