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
        cancellationToken.ThrowIfCancellationRequested();

        EffectivePermissionsFreshness deniedFreshness = EffectivePermissionsFreshness.SafeUnavailable(
            clock.UtcNow,
            "denied_safe");

        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(query.PrincipalId)
            || string.IsNullOrWhiteSpace(query.FolderId))
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
        IReadOnlyList<EffectivePermissionPrincipal> principalScopes = [EffectivePermissionPrincipal.User(query.PrincipalId)];

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
                SafeResult(EffectivePermissionsResultCode.ReadModelUnavailable, query.FolderId, query, readModelResult.Freshness with { Stale = true, ReasonCode = readModelResult.Freshness.ReasonCode ?? "projection_malformed" }),
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
            (EffectivePermissionsResultCode code, string reason) = snapshot.LifecycleState switch
            {
                EffectivePermissionsFolderLifecycleState.Unavailable => (EffectivePermissionsResultCode.ReadModelUnavailable, "lifecycle_unavailable"),
                EffectivePermissionsFolderLifecycleState.Malformed => (EffectivePermissionsResultCode.ReadModelUnavailable, "lifecycle_malformed"),
                EffectivePermissionsFolderLifecycleState.Stale => (EffectivePermissionsResultCode.ProjectionStale, "lifecycle_stale"),
                EffectivePermissionsFolderLifecycleState.Archived => (EffectivePermissionsResultCode.DeniedSafe, "lifecycle_archived"),
                EffectivePermissionsFolderLifecycleState.Missing => (EffectivePermissionsResultCode.NotFoundSafe, "lifecycle_missing"),
                _ => (EffectivePermissionsResultCode.DeniedSafe, "lifecycle_inactive"),
            };

            string? folderId = code == EffectivePermissionsResultCode.NotFoundSafe ? null : query.FolderId;

            return SafeResult(code, folderId, query, snapshot.Freshness with
            {
                Stale = snapshot.Freshness.Stale || snapshot.LifecycleState != EffectivePermissionsFolderLifecycleState.Active,
                ReasonCode = snapshot.Freshness.ReasonCode ?? reason,
            });
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
            .ThenBy(static row => row.Action, Comparer<string>.Create(EffectivePermissionsActionCatalog.CompareActions))
            .ThenBy(static row => (int)row.Source))
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

        bool workspaceRequiredButMissing = !string.IsNullOrWhiteSpace(taskScope.OpaqueWorkspaceId)
            && string.IsNullOrWhiteSpace(query.WorkspaceContextId);

        if (taskScope.Status != EffectivePermissionsTaskScopeStatus.Available
            || !string.Equals(taskScope.OpaqueTaskId, query.TaskContextId, StringComparison.Ordinal)
            || workspaceRequiredButMissing
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

        List<string> narrowedActions = [.. allowedActions.Where(action => taskScope.AllowedActions.Contains(action))];
        IReadOnlyList<EffectivePermissionLevel> permissions = EffectivePermissionsActionCatalog.ToPermissionLevels(narrowedActions);

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
    {
        if (query.ClientControlledTenantIds is null || query.ClientControlledTenantIds.Count == 0)
        {
            return false;
        }

        string authoritative = (query.AuthoritativeTenantId ?? string.Empty).Trim();
        string? firstNonEmpty = null;

        foreach (string? raw in query.ClientControlledTenantIds.Values)
        {
            string value = (raw ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                continue;
            }

            if (!string.Equals(value, authoritative, StringComparison.Ordinal))
            {
                return true;
            }

            if (firstNonEmpty is not null && !string.Equals(value, firstNonEmpty, StringComparison.Ordinal))
            {
                return true;
            }

            firstNonEmpty ??= value;
        }

        return false;
    }

    private static EffectivePermissionsResultCode MapTenantFailure(TenantAccessOutcome outcome)
        => outcome switch
        {
            TenantAccessOutcome.MissingAuthoritativeTenant => EffectivePermissionsResultCode.AuthenticationRequired,
            TenantAccessOutcome.StaleProjection or TenantAccessOutcome.UnavailableProjection => EffectivePermissionsResultCode.ReadModelUnavailable,
            TenantAccessOutcome.MalformedEvidence or TenantAccessOutcome.ReplayConflict => EffectivePermissionsResultCode.ReadModelUnavailable,
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
