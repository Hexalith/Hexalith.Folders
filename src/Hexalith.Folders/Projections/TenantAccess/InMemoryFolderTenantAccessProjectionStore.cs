using System.Collections.Concurrent;

namespace Hexalith.Folders.Projections.TenantAccess;

public sealed class InMemoryFolderTenantAccessProjectionStore : IFolderTenantAccessProjectionStore
{
    private readonly ConcurrentDictionary<string, FolderTenantAccessProjection> _projections = new(StringComparer.Ordinal);
    private readonly Lock _saveLock = new();

    public Task<FolderTenantAccessProjection?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        cancellationToken.ThrowIfCancellationRequested();

        _ = _projections.TryGetValue(tenantId, out FolderTenantAccessProjection? projection);
        return Task.FromResult(projection?.Clone());
    }

    public Task SaveAsync(FolderTenantAccessProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentException.ThrowIfNullOrWhiteSpace(projection.TenantId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_saveLock)
        {
            long currentVersion = _projections.TryGetValue(projection.TenantId, out FolderTenantAccessProjection? existing)
                ? existing.Version
                : 0L;

            if (projection.Version != currentVersion)
            {
                throw new TenantAccessConcurrencyException(projection.TenantId, projection.Version, currentVersion);
            }

            FolderTenantAccessProjection snapshot = projection.Clone();
            snapshot.Version = currentVersion + 1L;
            _projections[projection.TenantId] = snapshot;
        }

        return Task.CompletedTask;
    }
}
