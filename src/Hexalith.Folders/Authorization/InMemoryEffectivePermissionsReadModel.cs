namespace Hexalith.Folders.Authorization;

public sealed class InMemoryEffectivePermissionsReadModel : IEffectivePermissionsReadModel
{
    private readonly Dictionary<string, EffectivePermissionsReadModelSnapshot> _snapshots = new(StringComparer.Ordinal);

    public void Save(EffectivePermissionsReadModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _snapshots[Key(snapshot.ManagedTenantId, snapshot.FolderId)] = snapshot;
    }

    public Task<EffectivePermissionsReadModelResult> GetAsync(
        EffectivePermissionsReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_snapshots.TryGetValue(Key(request.ManagedTenantId, request.FolderId), out EffectivePermissionsReadModelSnapshot? snapshot)
            ? EffectivePermissionsReadModelResult.Available(snapshot)
            : EffectivePermissionsReadModelResult.NotFound(
                EffectivePermissionsFreshness.SafeUnavailable(DateTimeOffset.UnixEpoch, "folder_projection_missing")));
    }

    private static string Key(string managedTenantId, string folderId)
        => string.Join('|', managedTenantId, folderId);
}
