using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Queries;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Tenants.UI.Services.Configuration;
using Hexalith.Tenants.UI.State.GlobalAdministrators;
using Hexalith.Tenants.UI.State.TenantAudit;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantDetail;

namespace Hexalith.Tenants.UI.Services.Gateways;

public interface ITenantsBffComposition {
    /// <summary>Resolves current circuit authority against one literal tenant detail's membership evidence.</summary>
    /// <param name="tenantId">Literal tenant scope.</param>
    /// <param name="detail">Authoritative tenant membership evidence.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Fail-closed metadata and membership authority.</returns>
    ValueTask<TenantMutationAuthorizationReflection> ResolveTenantMutationAuthorizationAsync(
        string tenantId,
        TenantDetail detail,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(TenantMutationAuthorizationReflection.Indeterminate);

    /// <summary>Redacts one direct tenant read after resolving current circuit authority.</summary>
    ValueTask<TenantCorrectionProjection> ComposeTenantCorrectionProjectionAsync(
        string tenantId,
        string targetUserId,
        TenantDetail rawDetail,
        ReadModelFreshnessState freshness,
        ProjectionLifecycleState lifecycle,
        QueryResponseProvenance provenance,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(TenantCorrectionProjection.Unavailable(tenantId, targetUserId));

    /// <summary>Redacts every requested target from one direct read and one current principal resolution.</summary>
    ValueTask<IReadOnlyList<TenantCorrectionProjection>> ComposeTenantCorrectionProjectionsAsync(
        string tenantId,
        IReadOnlyList<string> targetUserIds,
        TenantDetail rawDetail,
        ReadModelFreshnessState freshness,
        ProjectionLifecycleState lifecycle,
        QueryResponseProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetUserIds);
        return ValueTask.FromResult<IReadOnlyList<TenantCorrectionProjection>>(targetUserIds
            .Select(target => TenantCorrectionProjection.Unavailable(tenantId, target)).ToArray());
    }

    bool IsReadSurfaceConnected { get; }

    bool IsCommandSurfaceConnected { get; }

    /// <summary>Gets an approved local destination for requesting audit access, if configured.</summary>
    string? AuditPermissionRecoveryHref => null;

    /// <summary>
    /// Gets an approved local destination for escalation, if configured. It serves both failed audit reads (the audit
    /// page and its receipt) and the Escalate recovery of a command's audit availability control when the audit
    /// record is delayed, unavailable, or has no in-panel verification support.
    /// </summary>
    string? AuditEscalationRecoveryHref => null;

    /// <summary>Gets whether fixed-scope command dispatch is connected.</summary>
    bool IsGlobalAdministratorDispatchConnected => false;

    /// <summary>Gets whether command status lookup is connected.</summary>
    bool IsGlobalAdministratorStatusConnected => false;

    /// <summary>Gets whether fixed-scope projection requery is connected.</summary>
    bool IsGlobalAdministratorRequeryConnected => false;

    /// <summary>Gets whether the downstream grant consequence preview is ready.</summary>
    bool IsGlobalAdministratorGrantPreviewReady => false;

    /// <summary>Gets the specific support-safe reason when grant-preview localization is incomplete.</summary>
    string? GlobalAdministratorGrantPreviewUnavailableReasonKey => null;

    /// <summary>Gets the recovery paired with an incomplete grant-preview localization reason.</summary>
    string? GlobalAdministratorGrantPreviewRecoveryKey => null;

    /// <summary>Gets whether the downstream removal consequence preview is ready.</summary>
    bool IsGlobalAdministratorRemovePreviewReady => false;

    TenantLifecycleAuthorizationReflectionState LifecycleAuthorizationReflection
        => TenantLifecycleAuthorizationReflectionState.Indeterminate;

    TenantLifecycleAuthorizationReflectionState GlobalAdministratorsAuthorizationReflection
        => TenantLifecycleAuthorizationReflectionState.Indeterminate;

    /// <summary>
    /// Resolves global-administrator authorization from the current authoritative principal evidence.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for principal resolution.</param>
    /// <returns>The fail-closed authorization reflection state for the current principal.</returns>
    /// <remarks>
    /// The default fails closed rather than forwarding to <see cref="GlobalAdministratorsAuthorizationReflection"/>:
    /// that property reads the request principal only, which is the interpretation this seam exists to replace.
    /// Forwarding to it let an implementation silently inherit the discarded HTTP-only evidence.
    /// </remarks>
    ValueTask<TenantLifecycleAuthorizationReflectionState> ResolveGlobalAdministratorsAuthorizationAsync(
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(TenantLifecycleAuthorizationReflectionState.Indeterminate);

    /// <summary>Composes the complete, fixed-scope consequence preview for one literal grant target.</summary>
    /// <param name="targetUserId">Literal target identifier; no normalization is permitted.</param>
    /// <param name="completeSnapshot">Complete current fixed-scope projection evidence.</param>
    /// <param name="cancellationToken">Cancellation token for current authority resolution.</param>
    /// <returns>A complete preview, or a fail-closed preview with support-safe recovery keys.</returns>
    ValueTask<GlobalAdministratorGrantPreview> ComposeGlobalAdministratorGrantPreviewAsync(
        string targetUserId,
        GlobalAdministratorsSnapshot completeSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(completeSnapshot);
        return ValueTask.FromResult(GlobalAdministratorGrantPreview.Unavailable(targetUserId));
    }

    /// <summary>Composes the complete, fixed-scope consequence preview for one literal removal target.</summary>
    /// <param name="targetUserId">Literal target identifier; no normalization is permitted.</param>
    /// <param name="completeSnapshot">Complete current fixed-scope projection evidence.</param>
    /// <param name="cancellationToken">Cancellation token for current authority resolution.</param>
    /// <returns>A complete preview, or a fail-closed preview with support-safe recovery keys.</returns>
    ValueTask<GlobalAdministratorRemovePreview> ComposeGlobalAdministratorRemovePreviewAsync(
        string targetUserId,
        GlobalAdministratorsSnapshot completeSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(completeSnapshot);
        return ValueTask.FromResult(GlobalAdministratorRemovePreview.Unavailable(targetUserId));
    }

    /// <summary>
    /// Resolves tenant-lifecycle authorization from the current authoritative circuit principal.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for principal resolution.</param>
    /// <returns>The fail-closed authorization reflection state for the current principal.</returns>
    ValueTask<TenantLifecycleAuthorizationReflectionState> ResolveLifecycleAuthorizationAsync(
        CancellationToken cancellationToken = default)
        => ResolveGlobalAdministratorsAuthorizationAsync(cancellationToken);

    ValueTask<TenantConfigurationComposition> ComposeTenantDetailAsync(
        TenantDetail detail,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(detail);
        return ValueTask.FromResult(new TenantConfigurationComposition(
            TenantConfigurationSafeComposer.SanitizeDetail(detail),
            TenantConfigurationSafeModel.Unavailable(detail.TenantId),
            TenantConfigurationManagementContext.Unavailable(detail.TenantId, detail.Status)));
    }

    ValueTask<TenantConfigurationComposition> ReauthorizeTenantDetailAsync(
        TenantDetail sanitizedDetail,
        TenantConfigurationSafeModel safeModel,
        bool degraded,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(sanitizedDetail);
        ArgumentNullException.ThrowIfNull(safeModel);
        return ValueTask.FromResult(new TenantConfigurationComposition(
            TenantConfigurationSafeComposer.SanitizeDetail(sanitizedDetail),
            TenantConfigurationSafeModel.Unavailable(safeModel.TenantId),
            TenantConfigurationManagementContext.Unavailable(safeModel.TenantId, sanitizedDetail.Status)));
    }

    ValueTask<TenantConfigurationManagementContext> ReauthorizeConfigurationManagementAsync(
        string tenantId,
        TenantStatus tenantStatus,
        TenantConfigurationSafeModel safeModel,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(safeModel);
        return ValueTask.FromResult(TenantConfigurationManagementContext.Unavailable(tenantId, tenantStatus));
    }

    /// <summary>
    /// Reauthorizes configuration management from current circuit principal and sanitized detail evidence.
    /// </summary>
    /// <param name="sanitizedDetail">Current sanitized authoritative detail.</param>
    /// <param name="safeModel">Current safe configuration model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current role and namespace management context.</returns>
    ValueTask<TenantConfigurationManagementContext> ReauthorizeConfigurationManagementAsync(
        TenantDetail sanitizedDetail,
        TenantConfigurationSafeModel safeModel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sanitizedDetail);
        ArgumentNullException.ThrowIfNull(safeModel);
        return ReauthorizeConfigurationManagementAsync(
            sanitizedDetail.TenantId,
            sanitizedDetail.Status,
            safeModel,
            cancellationToken);
    }

    /// <summary>
    /// Composes safe per-action role, namespace, and preview evidence without reading claims in the component.
    /// </summary>
    /// <param name="sanitizedDetail">Current sanitized authoritative detail.</param>
    /// <param name="managementContext">Current configuration management evidence.</param>
    /// <param name="lifecycleAuthorization">Current circuit-derived lifecycle authorization.</param>
    /// <returns>Safe BFF evidence for the shared high-impact kernel.</returns>
    TenantHighImpactBffEvidence ComposeTenantHighImpactEvidence(
        TenantDetail sanitizedDetail,
        TenantConfigurationManagementContext managementContext,
        TenantLifecycleAuthorizationReflectionState lifecycleAuthorization)
    {
        ArgumentNullException.ThrowIfNull(sanitizedDetail);
        ArgumentNullException.ThrowIfNull(managementContext);

        bool sameTenant = string.Equals(
            sanitizedDetail.TenantId,
            managementContext.TenantId,
            StringComparison.Ordinal);
        bool previewReady = sameTenant
            && !string.IsNullOrWhiteSpace(sanitizedDetail.TenantId)
            && sanitizedDetail.Status is not TenantStatus.Unknown;
        TenantHighImpactAuthorityEvidence configurationAuthority = managementContext.AuthorityState switch
        {
            TenantConfigurationAuthorityState.TenantOwner
                or TenantConfigurationAuthorityState.GlobalAdministrator
                => TenantHighImpactAuthorityEvidence.Authorized,
            TenantConfigurationAuthorityState.MissingPermission
                => TenantHighImpactAuthorityEvidence.MissingPermission,
            _ => TenantHighImpactAuthorityEvidence.Indeterminate,
        };
        TenantHighImpactNamespaceScopeEvidence scope = !sameTenant || !managementContext.IsAvailable
            ? TenantHighImpactNamespaceScopeEvidence.Indeterminate
            : managementContext.IsGlobalAdministrator || managementContext.AuthorizedPrefixes.Count > 0
                ? TenantHighImpactNamespaceScopeEvidence.Authorized
                : TenantHighImpactNamespaceScopeEvidence.Missing;

        return new(
            lifecycleAuthorization switch
            {
                TenantLifecycleAuthorizationReflectionState.Authorized
                    => TenantHighImpactAuthorityEvidence.Authorized,
                TenantLifecycleAuthorizationReflectionState.MissingPermission
                    => TenantHighImpactAuthorityEvidence.MissingPermission,
                _ => TenantHighImpactAuthorityEvidence.Indeterminate,
            },
            configurationAuthority,
            scope,
            previewReady ? TenantHighImpactPreviewEvidence.Ready : TenantHighImpactPreviewEvidence.Missing,
            previewReady ? TenantHighImpactPreviewEvidence.Ready : TenantHighImpactPreviewEvidence.Missing);
    }

    /// <summary>
    /// Determines whether the current principal holds namespace authorization for a literal key.
    /// </summary>
    /// <remarks>
    /// Projection-proof comparison reads the raw configuration dictionary, so it needs its own policy
    /// gate rather than trusting that a caller already applied one. Namespace authorization is the
    /// correct gate here, not display approval: a key may legitimately be commanded under proven scope
    /// while remaining absent from the read model.
    /// </remarks>
    /// <param name="tenantId">Literal requested tenant identifier.</param>
    /// <param name="key">Literal configuration key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the key is within proven scope; otherwise <see langword="false"/>.</returns>
    ValueTask<bool> IsConfigurationKeyAuthorizedAsync(
        string tenantId,
        string key,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(false);

    /// <summary>Composes an authorization-scoped, redacted preview from one fresh raw detail read.</summary>
    ValueTask<TenantSetConfigurationPreview> ComposeSetConfigurationPreviewAsync(
        TenantDetail rawDetail,
        TenantSetConfigurationIntent intent,
        ReadModelFreshnessState freshness,
        ProjectionLifecycleState lifecycle,
        string? projectionVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawDetail);
        ArgumentNullException.ThrowIfNull(intent);
        return ValueTask.FromResult(TenantSetConfigurationPreview.Unavailable(intent));
    }

    /// <summary>Composes an authorization-scoped, value-free remove preview from one fresh raw detail read.</summary>
    ValueTask<TenantRemoveConfigurationPreview> ComposeRemoveConfigurationPreviewAsync(
        TenantDetail rawDetail,
        TenantRemoveConfigurationIntent intent,
        ReadModelFreshnessState freshness,
        ProjectionLifecycleState lifecycle,
        string? projectionVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawDetail);
        ArgumentNullException.ThrowIfNull(intent);
        return ValueTask.FromResult(TenantRemoveConfigurationPreview.Unavailable(intent));
    }
}
