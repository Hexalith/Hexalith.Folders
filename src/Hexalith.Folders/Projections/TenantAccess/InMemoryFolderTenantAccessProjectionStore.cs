using System.Collections.Concurrent;

namespace Hexalith.Folders.Projections.TenantAccess;

public sealed class InMemoryFolderTenantAccessProjectionStore : IFolderTenantAccessProjectionStore
{
    private readonly ConcurrentDictionary<string, FolderTenantAccessProjection> _projections = new(StringComparer.Ordinal);

    public Task<FolderTenantAccessProjection?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        _ = _projections.TryGetValue(tenantId, out FolderTenantAccessProjection? projection);
        return Task.FromResult(projection?.Clone());
    }

    public Task SaveAsync(FolderTenantAccessProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentException.ThrowIfNullOrWhiteSpace(projection.TenantId);
        _projections[projection.TenantId] = projection.Clone();
        return Task.CompletedTask;
    }
}
