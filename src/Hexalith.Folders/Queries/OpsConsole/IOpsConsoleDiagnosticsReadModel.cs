namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Read model exposing the seven ops-console diagnostic projections. Production hosts populate it from the
/// diagnostic projections; the in-memory implementation is seeded directly for dev/test. All lookups are
/// tenant-scoped — a diagnostic owned by a different tenant must read as <c>null</c> (safe denial).
/// </summary>
public interface IOpsConsoleDiagnosticsReadModel
{
    /// <summary>Reads tenant-scoped readiness diagnostics, or <c>null</c> when none exist.</summary>
    Task<ReadinessDiagnosticsView?> GetReadinessAsync(string managedTenantId, CancellationToken cancellationToken = default);

    /// <summary>Reads workspace lock diagnostics, or <c>null</c> when none exist.</summary>
    Task<LockDiagnosticsView?> GetLockAsync(string managedTenantId, string folderId, string workspaceId, CancellationToken cancellationToken = default);

    /// <summary>Reads workspace dirty-state diagnostics, or <c>null</c> when none exist.</summary>
    Task<DirtyStateDiagnosticsView?> GetDirtyStateAsync(string managedTenantId, string folderId, string workspaceId, CancellationToken cancellationToken = default);

    /// <summary>Reads workspace failed-operation diagnostics, or <c>null</c> when none exist.</summary>
    Task<FailedOperationDiagnosticsView?> GetFailedOperationAsync(string managedTenantId, string folderId, string workspaceId, CancellationToken cancellationToken = default);

    /// <summary>Reads folder provider-status diagnostics, or <c>null</c> when none exist.</summary>
    Task<ProviderStatusDiagnosticsView?> GetProviderStatusAsync(string managedTenantId, string folderId, CancellationToken cancellationToken = default);

    /// <summary>Reads workspace sync-status diagnostics, or <c>null</c> when none exist.</summary>
    Task<SyncStatusDiagnosticsView?> GetSyncStatusAsync(string managedTenantId, string folderId, string workspaceId, CancellationToken cancellationToken = default);

    /// <summary>Reads tenant-scoped projection-freshness diagnostics, or <c>null</c> when none exist.</summary>
    Task<ProjectionFreshnessDiagnosticsView?> GetProjectionFreshnessAsync(string managedTenantId, CancellationToken cancellationToken = default);
}
