using System.Collections.Concurrent;

namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// In-memory <see cref="IOpsConsoleDiagnosticsReadModel"/> for dev/test hosts. Seeded directly via the
/// <c>Save</c> overloads; production hosts replace it with projection-backed read models. Keys are
/// tenant-scoped so a seeded diagnostic for one tenant is invisible to another.
/// </summary>
public sealed class InMemoryOpsConsoleDiagnosticsReadModel : IOpsConsoleDiagnosticsReadModel
{
    private readonly ConcurrentDictionary<string, ReadinessDiagnosticsView> _readiness = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, LockDiagnosticsView> _lock = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DirtyStateDiagnosticsView> _dirtyState = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, FailedOperationDiagnosticsView> _failedOperation = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ProviderStatusDiagnosticsView> _providerStatus = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SyncStatusDiagnosticsView> _syncStatus = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ProjectionFreshnessDiagnosticsView> _projectionFreshness = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task<ReadinessDiagnosticsView?> GetReadinessAsync(string managedTenantId, CancellationToken cancellationToken = default)
        => Get(_readiness, TenantKey(managedTenantId), cancellationToken);

    /// <inheritdoc/>
    public Task<LockDiagnosticsView?> GetLockAsync(string managedTenantId, string folderId, string workspaceId, CancellationToken cancellationToken = default)
        => Get(_lock, WorkspaceKey(managedTenantId, folderId, workspaceId), cancellationToken);

    /// <inheritdoc/>
    public Task<DirtyStateDiagnosticsView?> GetDirtyStateAsync(string managedTenantId, string folderId, string workspaceId, CancellationToken cancellationToken = default)
        => Get(_dirtyState, WorkspaceKey(managedTenantId, folderId, workspaceId), cancellationToken);

    /// <inheritdoc/>
    public Task<FailedOperationDiagnosticsView?> GetFailedOperationAsync(string managedTenantId, string folderId, string workspaceId, CancellationToken cancellationToken = default)
        => Get(_failedOperation, WorkspaceKey(managedTenantId, folderId, workspaceId), cancellationToken);

    /// <inheritdoc/>
    public Task<ProviderStatusDiagnosticsView?> GetProviderStatusAsync(string managedTenantId, string folderId, CancellationToken cancellationToken = default)
        => Get(_providerStatus, FolderKey(managedTenantId, folderId), cancellationToken);

    /// <inheritdoc/>
    public Task<SyncStatusDiagnosticsView?> GetSyncStatusAsync(string managedTenantId, string folderId, string workspaceId, CancellationToken cancellationToken = default)
        => Get(_syncStatus, WorkspaceKey(managedTenantId, folderId, workspaceId), cancellationToken);

    /// <inheritdoc/>
    public Task<ProjectionFreshnessDiagnosticsView?> GetProjectionFreshnessAsync(string managedTenantId, CancellationToken cancellationToken = default)
        => Get(_projectionFreshness, TenantKey(managedTenantId), cancellationToken);

    /// <summary>Seeds or replaces readiness diagnostics.</summary>
    public void Save(ReadinessDiagnosticsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _readiness[TenantKey(view.ManagedTenantId)] = view;
    }

    /// <summary>Seeds or replaces lock diagnostics.</summary>
    public void Save(LockDiagnosticsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _lock[WorkspaceKey(view.ManagedTenantId, view.FolderId, view.WorkspaceId)] = view;
    }

    /// <summary>Seeds or replaces dirty-state diagnostics.</summary>
    public void Save(DirtyStateDiagnosticsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _dirtyState[WorkspaceKey(view.ManagedTenantId, view.FolderId, view.WorkspaceId)] = view;
    }

    /// <summary>Seeds or replaces failed-operation diagnostics.</summary>
    public void Save(FailedOperationDiagnosticsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _failedOperation[WorkspaceKey(view.ManagedTenantId, view.FolderId, view.WorkspaceId)] = view;
    }

    /// <summary>Seeds or replaces provider-status diagnostics.</summary>
    public void Save(ProviderStatusDiagnosticsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _providerStatus[FolderKey(view.ManagedTenantId, view.FolderId)] = view;
    }

    /// <summary>Seeds or replaces sync-status diagnostics.</summary>
    public void Save(SyncStatusDiagnosticsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _syncStatus[WorkspaceKey(view.ManagedTenantId, view.FolderId, view.WorkspaceId)] = view;
    }

    /// <summary>Seeds or replaces projection-freshness diagnostics.</summary>
    public void Save(ProjectionFreshnessDiagnosticsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _projectionFreshness[TenantKey(view.ManagedTenantId)] = view;
    }

    private static Task<T?> Get<T>(ConcurrentDictionary<string, T> store, string key, CancellationToken cancellationToken)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        store.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    private static string TenantKey(string managedTenantId) => managedTenantId;

    private static string FolderKey(string managedTenantId, string folderId) => $"{managedTenantId}|{folderId}";

    private static string WorkspaceKey(string managedTenantId, string folderId, string workspaceId)
        => $"{managedTenantId}|{folderId}|{workspaceId}";
}
