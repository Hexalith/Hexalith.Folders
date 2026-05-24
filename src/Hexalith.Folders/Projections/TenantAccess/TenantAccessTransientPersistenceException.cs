namespace Hexalith.Folders.Projections.TenantAccess;

public sealed class TenantAccessTransientPersistenceException : Exception
{
    public TenantAccessTransientPersistenceException(string tenantId, Exception? innerException = null)
        : base($"Transient tenant-access projection persistence failure for tenant '{tenantId}'.", innerException)
        => TenantId = tenantId;

    public string TenantId { get; }
}
