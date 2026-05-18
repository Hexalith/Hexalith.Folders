namespace Hexalith.Folders.Projections.TenantAccess;

public interface IFolderTenantAccessProjectionStore
{
    Task<FolderTenantAccessProjection?> GetAsync(string tenantId, CancellationToken cancellationToken = default);

    Task SaveAsync(FolderTenantAccessProjection projection, CancellationToken cancellationToken = default);
}
