using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.IntegrationTests.Routing;

/// <summary>Counts tenant-access reads so a test can prove a request was rejected before any lookup.</summary>
/// <param name="inner">The seeded in-memory store.</param>
internal sealed class CountingFolderTenantAccessProjectionStore(InMemoryFolderTenantAccessProjectionStore inner)
    : IFolderTenantAccessProjectionStore
{
    private int _reads;

    /// <summary>Gets the number of tenant-access reads.</summary>
    public int Reads => Volatile.Read(ref _reads);

    /// <inheritdoc/>
    public Task<FolderTenantAccessProjection?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        _ = Interlocked.Increment(ref _reads);
        return inner.GetAsync(tenantId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SaveAsync(FolderTenantAccessProjection projection, CancellationToken cancellationToken = default)
        => inner.SaveAsync(projection, cancellationToken);
}
