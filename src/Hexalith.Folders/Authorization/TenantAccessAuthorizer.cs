using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Authorization;

public sealed class TenantAccessAuthorizer(
    IFolderTenantAccessProjectionStore store,
    IUtcClock clock,
    TenantAccessOptions options)
{
    public async Task<TenantAccessAuthorizationResult> AuthorizeMutationAsync(
        TenantAccessAuthorizationContext context,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(context, allowBoundedStale: false, cancellationToken).ConfigureAwait(false);

    public async Task<TenantAccessAuthorizationResult> AuthorizeDiagnosticReadAsync(
        TenantAccessAuthorizationContext context,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(context, allowBoundedStale: true, cancellationToken).ConfigureAwait(false);

    /// <summary>Validates current tenant-owner evidence without reusing an earlier authorization.</summary>
    public async Task<TenantAccessAuthorizationResult> AuthorizeOwnerMutationAsync(
        TenantAccessAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        using IDisposable suppression = PreauthorizedRequestContext.SuppressReuse();
        return await AuthorizeAsync(context, allowBoundedStale: false, cancellationToken,
            requiredRole: "TenantOwner").ConfigureAwait(false);
    }

    private async Task<TenantAccessAuthorizationResult> AuthorizeAsync(
        TenantAccessAuthorizationContext context,
        bool allowBoundedStale,
        CancellationToken cancellationToken,
        string? requiredRole = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (PreauthorizedRequestContext.Covers(context.AuthoritativeTenantId, context.PrincipalId))
        {
            return Result(
                TenantAccessOutcome.Allowed,
                context.AuthoritativeTenantId,
                freshnessStatus: TenantProjectionFreshnessStatus.Fresh,
                source: "preauthorized-request");
        }

        if (string.IsNullOrWhiteSpace(context.AuthoritativeTenantId))
        {
            return Result(TenantAccessOutcome.MissingAuthoritativeTenant, (string?)null);
        }

        if (!string.IsNullOrWhiteSpace(context.RequestedTenantId)
            && !string.Equals(context.AuthoritativeTenantId, context.RequestedTenantId, StringComparison.Ordinal))
        {
            return Result(TenantAccessOutcome.TenantMismatch, context.AuthoritativeTenantId);
        }

        FolderTenantAccessProjection? projection;
        try
        {
            projection = await store.GetAsync(context.AuthoritativeTenantId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result(
                TenantAccessOutcome.UnavailableProjection,
                context.AuthoritativeTenantId,
                freshnessStatus: TenantProjectionFreshnessStatus.Unavailable,
                source: "projection-store");
        }

        if (projection is null)
        {
            return Result(TenantAccessOutcome.UnknownTenant, context.AuthoritativeTenantId);
        }

        if (projection.ReplayConflict)
        {
            return Result(TenantAccessOutcome.ReplayConflict, projection);
        }

        if (projection.MalformedEvidence || projection.LastEventTimestamp is null
            || !string.Equals(projection.TenantId, context.AuthoritativeTenantId, StringComparison.Ordinal)
            || projection.Principals is null
            || projection.Principals.Any(entry => entry.Value is null
                || !string.Equals(entry.Key, entry.Value.PrincipalId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(entry.Value.Role)))
        {
            return Result(TenantAccessOutcome.MalformedEvidence, projection);
        }

        TimeSpan age = clock.UtcNow - projection.LastEventTimestamp.Value;
        if (age < TimeSpan.Zero)
        {
            return Result(TenantAccessOutcome.MalformedEvidence, projection, age, TenantProjectionFreshnessStatus.Future);
        }

        TenantProjectionFreshnessStatus freshness = age <= options.MutationFreshnessBudget
            ? TenantProjectionFreshnessStatus.Fresh
            : TenantProjectionFreshnessStatus.Stale;

        if (!allowBoundedStale && freshness == TenantProjectionFreshnessStatus.Stale)
        {
            return Result(TenantAccessOutcome.StaleProjection, projection, age, freshness);
        }

        if (allowBoundedStale && age > options.DiagnosticStalenessBudget)
        {
            return Result(TenantAccessOutcome.StaleProjection, projection, age, freshness);
        }

        if (!projection.Enabled)
        {
            return Result(TenantAccessOutcome.DisabledTenant, projection, age, freshness);
        }

        if (string.IsNullOrWhiteSpace(context.PrincipalId)
            || !projection.Principals.TryGetValue(context.PrincipalId, out FolderTenantPrincipalEvidence? principal)
            || requiredRole is not null && !string.Equals(principal.Role, requiredRole, StringComparison.Ordinal))
        {
            return Result(TenantAccessOutcome.Denied, projection, age, freshness);
        }

        return Result(TenantAccessOutcome.Allowed, projection, age, freshness);
    }

    private static TenantAccessAuthorizationResult Result(
        TenantAccessOutcome outcome,
        string? tenantId,
        TenantProjectionFreshnessStatus freshnessStatus = TenantProjectionFreshnessStatus.Unknown,
        string source = "local-projection")
        => new(outcome, Code(outcome), tenantId, null, null, null, freshnessStatus, source);

    private static TenantAccessAuthorizationResult Result(
        TenantAccessOutcome outcome,
        FolderTenantAccessProjection projection,
        TimeSpan? age = null,
        TenantProjectionFreshnessStatus freshnessStatus = TenantProjectionFreshnessStatus.Unknown)
        => new(
            outcome,
            Code(outcome),
            projection.TenantId,
            projection.ProjectionWatermark,
            projection.LastEventTimestamp,
            age,
            freshnessStatus,
            "local-projection");

    private static string Code(TenantAccessOutcome outcome)
        => outcome switch
        {
            TenantAccessOutcome.Allowed => "allowed",
            TenantAccessOutcome.Denied => "denied",
            TenantAccessOutcome.StaleProjection => "stale_projection",
            TenantAccessOutcome.UnavailableProjection => "unavailable_projection",
            TenantAccessOutcome.UnknownTenant => "unknown_tenant",
            TenantAccessOutcome.DisabledTenant => "disabled_tenant",
            TenantAccessOutcome.MalformedEvidence => "malformed_evidence",
            TenantAccessOutcome.TenantMismatch => "tenant_mismatch",
            TenantAccessOutcome.MissingAuthoritativeTenant => "missing_authoritative_tenant",
            TenantAccessOutcome.ReplayConflict => "replay_conflict",
            _ => "denied",
        };
}
