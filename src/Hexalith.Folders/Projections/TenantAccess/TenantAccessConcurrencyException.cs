namespace Hexalith.Folders.Projections.TenantAccess;

/// <summary>
/// Thrown by <see cref="IFolderTenantAccessProjectionStore.SaveAsync"/> when the projection's
/// <see cref="FolderTenantAccessProjection.Version"/> does not match the currently-stored version.
/// Callers retry the read-modify-write cycle a bounded number of times.
/// </summary>
public sealed class TenantAccessConcurrencyException : Exception
{
    public TenantAccessConcurrencyException(string tenantId, long expectedVersion, long actualVersion)
        : base($"Optimistic concurrency conflict for tenant '{tenantId}': expected version {expectedVersion}, store has {actualVersion}.")
    {
        TenantId = tenantId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    public string TenantId { get; }

    public long ExpectedVersion { get; }

    public long ActualVersion { get; }
}
