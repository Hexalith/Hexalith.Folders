using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Authorization;

public sealed class EffectivePermissionsFolderPermissionEvidenceProvider(
    IEffectivePermissionsReadModel readModel,
    IUtcClock clock) : IFolderPermissionEvidenceProvider
{
    public async Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
        FolderPermissionEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!EffectivePermissionsActionCatalog.IsSupported(request.ActionToken)
            || string.IsNullOrWhiteSpace(request.ManagedTenantId)
            || string.IsNullOrWhiteSpace(request.PrincipalId)
            || string.IsNullOrWhiteSpace(request.OperationScope))
        {
            return FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Malformed, null);
        }

        EffectivePermissionsReadModelResult result;
        try
        {
            result = await readModel.GetAsync(
                new EffectivePermissionsReadModelRequest(
                    request.ManagedTenantId,
                    request.OperationScope,
                    [EffectivePermissionPrincipal.User(request.PrincipalId)],
                    request.TaskId,
                    WorkspaceContextId: null,
                    ReadConsistency: "read_your_writes"),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Unavailable, null);
        }

        return result.Status switch
        {
            EffectivePermissionsReadModelStatus.Available when result.Snapshot is not null =>
                FromSnapshot(request, result.Snapshot),
            EffectivePermissionsReadModelStatus.Available =>
                FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Malformed, null),
            EffectivePermissionsReadModelStatus.Stale when result.Snapshot is not null && request.AllowBoundedStale =>
                FromSnapshot(request, result.Snapshot),
            EffectivePermissionsReadModelStatus.Stale =>
                FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Stale, result.Freshness.ProjectionWatermark),
            EffectivePermissionsReadModelStatus.NotFound =>
                FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.NotFoundSafe, null),
            EffectivePermissionsReadModelStatus.Malformed =>
                FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Malformed, null),
            EffectivePermissionsReadModelStatus.Unavailable =>
                FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Unavailable, null),
            _ => FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Unavailable, null),
        };
    }

    private FolderPermissionEvidenceResult FromSnapshot(
        FolderPermissionEvidenceRequest request,
        EffectivePermissionsReadModelSnapshot snapshot)
    {
        if (snapshot.LifecycleState == EffectivePermissionsFolderLifecycleState.Missing)
        {
            return FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.NotFoundSafe, null);
        }

        if (snapshot.LifecycleState is EffectivePermissionsFolderLifecycleState.Unavailable)
        {
            return FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Unavailable, null);
        }

        if (snapshot.LifecycleState is EffectivePermissionsFolderLifecycleState.Malformed)
        {
            return FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Malformed, null);
        }

        if (snapshot.LifecycleState is not EffectivePermissionsFolderLifecycleState.Active)
        {
            return FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Denied, snapshot.Freshness.ProjectionWatermark);
        }

        if (snapshot.Freshness.ObservedAt > clock.UtcNow)
        {
            return FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Malformed, null);
        }

        bool stale = snapshot.Freshness.Stale || !snapshot.RevocationFreshnessEstablished;
        if (stale && !request.AllowBoundedStale)
        {
            return FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Stale, snapshot.Freshness.ProjectionWatermark);
        }

        if (!HasActionGrant(request, snapshot))
        {
            return FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Denied, snapshot.Freshness.ProjectionWatermark);
        }

        return FolderPermissionEvidenceResult.Allowed(
            snapshot.Freshness.ProjectionWatermark,
            stale ? "bounded_stale" : "fresh",
            snapshot.OrganizationId);
    }

    private static bool HasActionGrant(
        FolderPermissionEvidenceRequest request,
        EffectivePermissionsReadModelSnapshot snapshot)
    {
        EffectivePermissionPrincipal principal = EffectivePermissionPrincipal.User(request.PrincipalId);
        bool granted = false;

        foreach (EffectivePermissionEvidenceRow row in snapshot.EvidenceRows
            .Where(row => row.Principal == principal && string.Equals(row.Action, request.ActionToken, StringComparison.Ordinal))
            .OrderBy(static row => row.Sequence)
            .ThenBy(static row => row.EffectiveAt)
            .ThenBy(static row => row.Source == EffectivePermissionEvidenceSource.FolderOverrideRevoke ? 1 : 0))
        {
            granted = row.Source != EffectivePermissionEvidenceSource.FolderOverrideRevoke;
        }

        return granted;
    }
}
