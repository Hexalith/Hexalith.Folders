namespace Hexalith.Folders.Projections.TenantAccess;

public enum FolderTenantAccessEventKind
{
    TenantCreated,
    TenantUpdated,
    TenantDisabled,
    TenantEnabled,
    UserAddedToTenant,
    UserRemovedFromTenant,
    UserRoleChanged,
    TenantConfigurationSet,
    TenantConfigurationRemoved,
}
