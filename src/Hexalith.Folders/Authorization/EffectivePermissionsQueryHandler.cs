using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Authorization;

public sealed class EffectivePermissionsQueryHandler(
    TenantAccessAuthorizer tenantAccessAuthorizer,
    IEffectivePermissionsReadModel readModel,
    IUtcClock clock)
{
    private const string OperationId = "GetEffectivePermissions";
    private const string AllowedOutcome = "allowed";
    private const string DeniedSafeOutcome = "denied_safe";
    private const string ReadYourWrites = "read_your_writes";

    public async Task<EffectivePermissionsQueryResult> HandleAsync(
        EffectivePermissionsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        EffectivePermissionsFreshness deniedFreshness = EffectivePermissionsFreshness.SafeUnavailable(
            clock.UtcNow,
            "denied_safe");

        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId) || string.IsNullOrWhiteSpace(query.PrincipalId))
        {
            return SafeResult(EffectivePermissionsResultCode.AuthenticationRequired, null, query, deniedFreshness);
        }

        if (HasClientTenantMismatch(query))
        {
            return SafeResult(EffectivePermissionsResultCode.AuthorizationDenied, null, query, deniedFreshness);
        }

        TenantAccessAuthorizationResult tenantAccess = await tenantAccessAuthorizer.AuthorizeDiagnosticReadAsync(
            new TenantAccessAuthorizationContext(query.AuthoritativeTenantId, query.PrincipalId, RequestedTenantId: null),
            cancellationToken).ConfigureAwait(false);

        if (!tenantAccess.IsAllowed)
        {
            return SafeResult(MapTenantFailure(tenantAccess.Outcome), null, query, deniedFreshness);
        }

        string managedTenantId = tenantAccess.TenantId ?? query.AuthoritativeTenantId;
        IReadOnlyList<EffectivePermissionPrincipal> principalScopes = PrincipalScopes(query);

        EffectivePermissionsReadModelResult readModelResult;
        try
        {
            readModelResult = await readModel.GetAsync(
                new EffectivePermissionsReadModelRequest(
                    managedTenantId,
                    query.FolderId,
                    principalScopes,
                    query.TaskContextId,
                    query.WorkspaceContextId,
                    ReadYourWrites),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SafeResult(
                EffectivePermissionsResultCode.ReadModelUnavailable,
                query.FolderId,
                query,
                EffectivePermissionsFreshness.SafeUnavailable(clock.UtcNow, "read_model_unavailable"));
        }

        return readModelResult.Status switch
        {
            EffectivePermissionsReadModelStatus.Available when readModelResult.Snapshot is not null =>
                Compute(query, principalScopes, readModelResult.Snapshot),
            EffectivePermissionsReadModelStatus.Stale =>
                SafeResult(EffectivePermissionsResultCode.ProjectionStale, query.FolderId, query, readModelResult.Freshness with { Stale = true }),
            EffectivePermissionsReadModelStatus.Unavailable =>
                SafeResult(EffectivePermissionsResultCode.ReadModelUnavailable, query.FolderId, query, readModelResult.Freshness with { Stale = true }),
            EffectivePermissionsReadModelStatus.Malformed =>
                SafeResult(EffectivePermissionsResultCode.ProjectionStale, query.FolderId, query, readModelResult.Freshness with { Stale = true }),
            EffectivePermissionsReadModelStatus.NotFound =>
                SafeResult(EffectivePermissionsResultCode.NotFoundSafe, null, query, readModelResult.Freshness),
            _ => SafeResult(EffectivePermissionsResultCode.ReadModelUnavailable, query.FolderId, query, readModelResult.Freshness),
        };
    }

    private static EffectivePermissionsQueryResult Compute(
        EffectivePermissionsQuery query,
        IReadOnlyList<EffectivePermissionPrincipal> principalScopes,
        EffectivePermissionsReadModelSnapshot snapshot)
    {
        if (snapshot.LifecycleState != EffectivePermissionsFolderLifecycleState.Active)
        {
            EffectivePermissionsResultCode code = snapshot.LifecycleState is EffectivePermissionsFolderLifecycleState.Unavailable
                ? EffectivePermissionsResultCode.ReadModelUnavailable
                : EffectivePermissionsResultCode.DeniedSafe;

            return SafeResult(code, query.FolderId, query, snapshot.Freshness with { Stale = snapshot.Freshness.Stale || snapshot.LifecycleState != EffectivePermissionsFolderLifecycleState.Active });
        }

        if (!snapshot.RevocationFreshnessEstablished)
        {
            return SafeResult(
                EffectivePermissionsResultCode.ProjectionStale,
                query.FolderId,
                query,
                snapshot.Freshness with
                {
                    Stale = true,
                    ReasonCode = snapshot.Freshness.ReasonCode ?? "revocation_freshness_unproven",
                });
        }

        HashSet<EffectivePermissionTuple> grants = [];
        HashSet<EffectivePermissionTuple> revokes = [];
        HashSet<EffectivePermissionPrincipal> principals = new(principalScopes);

        foreach (EffectivePermissionEvidenceRow row in snapshot.EvidenceRows
            .Where(static row => EffectivePermissionsActionCatalog.IsSupported(row.Action))
            .OrderBy(static row => row.Sequence)
            .ThenBy(static row => row.EffectiveAt)
            .ThenBy(static row => row.Action, Comparer<string>.Create(EffectivePermissionsActionCatalog.CompareActions)))
        {
            if (!principals.Contains(row.Principal))
            {
                continue;
            }

            EffectivePermissionTuple tuple = new(row.Principal, row.Action);
            if (row.Source == EffectivePermissionEvidenceSource.FolderOverrideRevoke)
            {
                revokes.Add(tuple);
                grants.Remove(tuple);
                continue;
            }

            grants.Add(tuple);
        }

        List<string> allowedActions = grants
            .Where(tuple => !revokes.Contains(tuple))
            .Select(static tuple => tuple.Action)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static action => action, Comparer<string>.Create(EffectivePermissionsActionCatalog.CompareActions))
            .ToList();

        EffectivePermissionsQueryResult? taskScopeResult = ApplyTaskScope(query, snapshot, allowedActions);
        if (taskScopeResult is not null)
        {
            return taskScopeResult;
        }

        IReadOnlyList<EffectivePermissionLevel> permissions = EffectivePermissionsActionCatalog.ToPermissionLevels(allowedActions);
        EffectivePermissionsResultCode resultCode = permissions.Count == 0
            ? EffectivePermissionsResultCode.DeniedSafe
            : EffectivePermissionsResultCode.Allowed;

        return new EffectivePermissionsQueryResult(
            resultCode,
            query.FolderId,
            permissions,
            permissions.Count == 0 ? DeniedSafeOutcome : AllowedOutcome,
            snapshot.Freshness,
            query.CorrelationId,
            OperationId,
            TaskContextId: null,
            WorkspaceContextId: null);
    }

    private static EffectivePermissionsQueryResult? ApplyTaskScope(
        EffectivePermissionsQuery query,
        EffectivePermissionsReadModelSnapshot snapshot,
        List<string> allowedActions)
    {
        if (string.IsNullOrWhiteSpace(query.TaskContextId))
        {
            return null;
        }

        EffectivePermissionsTaskScope? taskScope = snapshot.TaskScope;
        if (taskScope is null)
        {
            return SafeResult(EffectivePermissionsResultCode.DeniedSafe, query.FolderId, query, snapshot.Freshness);
        }

        if (taskScope.Status == EffectivePermissionsTaskScopeStatus.Unavailable)
        {
            return SafeResult(
                EffectivePermissionsResultCode.ReadModelUnavailable,
                query.FolderId,
                query,
                snapshot.Freshness with { Stale = true, ReasonCode = snapshot.Freshness.ReasonCode ?? "task_scope_unavailable" });
        }

        if (taskScope.Status != EffectivePermissionsTaskScopeStatus.Available
            || !string.Equals(taskScope.OpaqueTaskId, query.TaskContextId, StringComparison.Ordinal)
            || (!string.IsNullOrWhiteSpace(query.WorkspaceContextId)
                && !string.Equals(taskScope.OpaqueWorkspaceId, query.WorkspaceContextId, StringComparison.Ordinal)))
        {
            return SafeResult(
                EffectivePermissionsResultCode.DeniedSafe,
                query.FolderId,
                query,
                snapshot.Freshness with
                {
                    Stale = snapshot.Freshness.Stale || taskScope.Status == EffectivePermissionsTaskScopeStatus.Stale,
                    ReasonCode = snapshot.Freshness.ReasonCode ?? "task_scope_denied_safe",
                });
        }

        allowedActions.RemoveAll(action => !taskScope.AllowedActions.Contains(action));
        IReadOnlyList<EffectivePermissionLevel> permissions = EffectivePermissionsActionCatalog.ToPermissionLevels(allowedActions);

        return new EffectivePermissionsQueryResult(
            permissions.Count == 0 ? EffectivePermissionsResultCode.DeniedSafe : EffectivePermissionsResultCode.Allowed,
            query.FolderId,
            permissions,
            permissions.Count == 0 ? DeniedSafeOutcome : AllowedOutcome,
            snapshot.Freshness,
            query.CorrelationId,
            OperationId,
            taskScope.OpaqueTaskId,
            taskScope.OpaqueWorkspaceId);
    }

    private static bool HasClientTenantMismatch(EffectivePermissionsQuery query)
        => query.ClientControlledTenantIds?.Values.Any(value =>
            !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, query.AuthoritativeTenantId, StringComparison.Ordinal)) == true;

    private static IReadOnlyList<EffectivePermissionPrincipal> PrincipalScopes(EffectivePermissionsQuery query)
        => query.PrincipalScopes is { Count: > 0 }
            ? query.PrincipalScopes
            : [EffectivePermissionPrincipal.User(query.PrincipalId)];

    private static EffectivePermissionsResultCode MapTenantFailure(TenantAccessOutcome outcome)
        => outcome switch
        {
            TenantAccessOutcome.MissingAuthoritativeTenant => EffectivePermissionsResultCode.AuthenticationRequired,
            TenantAccessOutcome.StaleProjection or TenantAccessOutcome.UnavailableProjection => EffectivePermissionsResultCode.ReadModelUnavailable,
            TenantAccessOutcome.UnknownTenant => EffectivePermissionsResultCode.NotFoundSafe,
            _ => EffectivePermissionsResultCode.AuthorizationDenied,
        };

    private static EffectivePermissionsQueryResult SafeResult(
        EffectivePermissionsResultCode code,
        string? folderId,
        EffectivePermissionsQuery query,
        EffectivePermissionsFreshness freshness)
        => new(
            code,
            folderId,
            [],
            DeniedSafeOutcome,
            freshness,
            query.CorrelationId,
            OperationId,
            TaskContextId: null,
            WorkspaceContextId: null);

    private sealed record EffectivePermissionTuple(
        EffectivePermissionPrincipal Principal,
        string Action);
}
