using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Folders.Projections.TenantAccess;

/// <summary>Shares the tenant authorization projection across Folders hosts through the EventStore read-model store.</summary>
public sealed class EventStoreFolderTenantAccessProjectionStore(IReadModelStore store)
    : IFolderTenantAccessProjectionStore
{
    private const string StateStoreName = "statestore";
    private const string KeyPrefix = "projection:folders:tenant-access:";

    public async Task<FolderTenantAccessProjection?> GetAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ReadModelEntry<FolderTenantAccessProjection> entry = await store
            .GetAsync<FolderTenantAccessProjection>(StateStoreName, KeyPrefix + tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (entry.Value is not { } projection)
        {
            return null;
        }

        return string.Equals(projection.TenantId, tenantId, StringComparison.Ordinal)
            ? projection
            : throw new InvalidOperationException("Tenant access projection identity does not match its storage key.");
    }

    public async Task SaveAsync(
        FolderTenantAccessProjection projection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentException.ThrowIfNullOrWhiteSpace(projection.TenantId);
        string key = KeyPrefix + projection.TenantId;
        ReadModelEntry<FolderTenantAccessProjection> existing = await store
            .GetAsync<FolderTenantAccessProjection>(StateStoreName, key, cancellationToken)
            .ConfigureAwait(false);
        long currentVersion = existing.Value?.Version ?? 0L;
        if (existing.Value is { } current
            && !string.Equals(current.TenantId, projection.TenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Tenant access projection identity does not match its storage key.");
        }

        if (projection.Version != currentVersion)
        {
            throw new TenantAccessConcurrencyException(projection.TenantId, projection.Version, currentVersion);
        }

        FolderTenantAccessProjection snapshot = projection.Clone();
        snapshot.Version = currentVersion + 1;
        bool saved = await store
            .TrySaveAsync(StateStoreName, key, snapshot, existing.ETag ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);
        if (!saved)
        {
            throw new TenantAccessConcurrencyException(projection.TenantId, projection.Version, currentVersion);
        }
    }
}
