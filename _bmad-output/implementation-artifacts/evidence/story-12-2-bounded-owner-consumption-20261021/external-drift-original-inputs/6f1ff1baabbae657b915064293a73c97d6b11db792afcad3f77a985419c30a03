using Hexalith.EventStore.Client.Projections;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.UI.State.TenantCommands;

namespace Hexalith.Tenants.UI.State.TenantDetail;

/// <summary>
/// Carries the lifecycle compatibility result together with the exact shared-kernel evidence that produced it.
/// </summary>
/// <param name="TenantId">Literal tenant identifier.</param>
/// <param name="CurrentStatus">Last-confirmed lifecycle status.</param>
/// <param name="Operation">Lifecycle operation.</param>
/// <param name="Freshness">Compatibility projection freshness.</param>
/// <param name="SurfaceKind">Detail surface state.</param>
/// <param name="IsCommandSurfaceConnected">Whether action-specific command support is connected.</param>
/// <param name="GovernanceReadiness">Compatibility admission readiness.</param>
/// <param name="AuthorizationReflection">Compatibility server-reflected authority.</param>
/// <param name="IsUnavailable">Whether the requested stage is unavailable.</param>
/// <param name="UnavailableReasonCategory">Compatibility reason category.</param>
/// <param name="SafeMessageKey">Whole-string localized result key.</param>
/// <param name="ExpectedDomainOutcomeKey">Optional safe expected-domain-outcome key.</param>
/// <param name="FocusTarget">Focus destination for the result.</param>
/// <param name="LiveRegionPoliteness">Live-region politeness for the result.</param>
/// <param name="Evidence">Exact shared-kernel evidence retained for authoritative decisions and rendering.</param>
public sealed record TenantLifecycleAvailability(
    string TenantId,
    TenantStatus CurrentStatus,
    TenantLifecycleOperation Operation,
    ReadModelFreshnessState Freshness,
    TenantDetailSurfaceKind SurfaceKind,
    bool IsCommandSurfaceConnected,
    TenantLifecycleGovernanceReadiness GovernanceReadiness,
    TenantLifecycleAuthorizationReflectionState AuthorizationReflection,
    bool IsUnavailable,
    TenantLifecycleUnavailableReasonCategory UnavailableReasonCategory,
    string SafeMessageKey,
    string? ExpectedDomainOutcomeKey,
    TenantCommandFocusTarget FocusTarget,
    TenantCommandLiveRegionPoliteness LiveRegionPoliteness,
    TenantHighImpactActionEvidence? Evidence = null)
{
    /// <summary>
    /// Evaluates shared lifecycle evidence and derives the legacy compatibility fields when they are not supplied.
    /// </summary>
    /// <param name="evidence">Exact typed evidence.</param>
    /// <param name="governanceReadiness">Optional legacy admission override.</param>
    /// <param name="authorizationReflection">Optional legacy authority override.</param>
    /// <param name="preferDomainOutcome">Whether the legacy adapter should retain its same-state presentation.</param>
    /// <returns>The typed and compatibility lifecycle availability result.</returns>
    public static TenantLifecycleAvailability FromEvidence(
        TenantHighImpactActionEvidence evidence,
        TenantLifecycleGovernanceReadiness? governanceReadiness = null,
        TenantLifecycleAuthorizationReflectionState? authorizationReflection = null,
        bool preferDomainOutcome = false)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Action is not TenantHighImpactAction.EnableTenant
            and not TenantHighImpactAction.DisableTenant)
        {
            throw new ArgumentOutOfRangeException(nameof(evidence), evidence.Action, "Lifecycle availability requires a lifecycle action.");
        }

        TenantHighImpactActionAvailability result = TenantHighImpactActionAvailabilityEvaluator.Evaluate(evidence);
        TenantLifecycleOperation operation = evidence.Action is TenantHighImpactAction.EnableTenant
            ? TenantLifecycleOperation.EnableTenant
            : TenantLifecycleOperation.DisableTenant;
        TenantLifecycleGovernanceReadiness resolvedGovernance = governanceReadiness
            ?? evidence.Admission switch
            {
                TenantHighImpactAdmissionEvidence.Available => TenantLifecycleGovernanceReadiness.Ready,
                TenantHighImpactAdmissionEvidence.Busy => TenantLifecycleGovernanceReadiness.Blocked,
                _ => TenantLifecycleGovernanceReadiness.Unresolved,
            };
        TenantLifecycleAuthorizationReflectionState resolvedAuthorization = authorizationReflection
            ?? evidence.Authority switch
            {
                TenantHighImpactAuthorityEvidence.Authorized
                    => TenantLifecycleAuthorizationReflectionState.Authorized,
                TenantHighImpactAuthorityEvidence.MissingPermission
                    => TenantLifecycleAuthorizationReflectionState.MissingPermission,
                _ => TenantLifecycleAuthorizationReflectionState.Indeterminate,
            };
        bool legacyDomainOutcome = preferDomainOutcome
            && result.DomainOutcome is TenantHighImpactDomainOutcome.LifecycleStateAlreadySet
            && evidence.SurfaceKind is TenantDetailSurfaceKind.Ready
            && evidence.Freshness is TenantHighImpactFreshnessState.Current
                or TenantHighImpactFreshnessState.Aging
            && evidence.ProjectionLifecycle is Hexalith.EventStore.Contracts.Queries.ProjectionLifecycleState.Current;
        TenantLifecycleUnavailableReasonCategory category = legacyDomainOutcome
            ? TenantLifecycleUnavailableReasonCategory.MissingLifecycleSupport
            : result.UnavailableReason switch
        {
            TenantHighImpactUnavailableReason.MissingPermission => TenantLifecycleUnavailableReasonCategory.MissingPermission,
            TenantHighImpactUnavailableReason.StaleData => TenantLifecycleUnavailableReasonCategory.StaleData,
            TenantHighImpactUnavailableReason.MissingLifecycleSupport => TenantLifecycleUnavailableReasonCategory.MissingLifecycleSupport,
            TenantHighImpactUnavailableReason.HighImpactFlowNotReady
                or TenantHighImpactUnavailableReason.MissingConsequencePreview
                or TenantHighImpactUnavailableReason.MissingAuditProof
                => TenantLifecycleUnavailableReasonCategory.HighImpactFlowNotReady,
            _ when result.DomainOutcome is TenantHighImpactDomainOutcome.LifecycleStateAlreadySet
                => TenantLifecycleUnavailableReasonCategory.MissingLifecycleSupport,
            _ => TenantLifecycleUnavailableReasonCategory.None,
        };

        return new(
            evidence.TenantId,
            evidence.TenantStatus,
            operation,
            ToReadModelFreshness(evidence),
            evidence.SurfaceKind,
            evidence.Support is TenantHighImpactSupportEvidence.Ready,
            resolvedGovernance,
            resolvedAuthorization,
            IsUnavailable: legacyDomainOutcome || !result.IsEligible,
            category,
            legacyDomainOutcome
                ? operation is TenantLifecycleOperation.EnableTenant
                    ? "Tenants.Lifecycle.Unavailable.AlreadyActive"
                    : "Tenants.Lifecycle.Unavailable.AlreadyDisabled"
                : ResolveSafeMessageKey(evidence, result, operation),
            result.DomainOutcome is TenantHighImpactDomainOutcome.LifecycleStateAlreadySet
                ? "TenantLifecycleStateAlreadySet"
                : null,
            legacyDomainOutcome
                ? TenantCommandFocusTarget.Lifecycle
                : result.UnavailableReason is TenantHighImpactUnavailableReason.StaleData
                ? TenantCommandFocusTarget.Refresh
                : result.IsEligible
                    ? TenantCommandFocusTarget.Submit
                    : TenantCommandFocusTarget.Lifecycle,
            result.DomainOutcome is not TenantHighImpactDomainOutcome.None || result.IsEligible
                ? TenantCommandLiveRegionPoliteness.Polite
                : TenantCommandLiveRegionPoliteness.Assertive,
            evidence);
    }

    private static string ResolveSafeMessageKey(
        TenantHighImpactActionEvidence evidence,
        TenantHighImpactActionAvailability result,
        TenantLifecycleOperation operation)
    {
        if (result.DomainOutcome is TenantHighImpactDomainOutcome.LifecycleStateAlreadySet
            && result.UnavailableReason is TenantHighImpactUnavailableReason.None)
        {
            return operation is TenantLifecycleOperation.EnableTenant
                ? "Tenants.Lifecycle.Unavailable.AlreadyActive"
                : "Tenants.Lifecycle.Unavailable.AlreadyDisabled";
        }

        return result.UnavailableReason switch
        {
            TenantHighImpactUnavailableReason.StaleData
                when evidence.SurfaceKind is not TenantDetailSurfaceKind.Ready
                    and not TenantDetailSurfaceKind.Unauthorized
                    || evidence.Freshness is TenantHighImpactFreshnessState.Stale
                        or TenantHighImpactFreshnessState.Unknown
                    || evidence.Freshness is TenantHighImpactFreshnessState.Refreshing
                        && !evidence.HasCurrentBaseline
                => "Tenants.Lifecycle.Unavailable.StaleFreshness",
            TenantHighImpactUnavailableReason.StaleData => "Tenants.Lifecycle.Unavailable.ProjectionLifecycle",
            TenantHighImpactUnavailableReason.MissingPermission => "Tenants.Lifecycle.Unavailable.MissingPermission",
            TenantHighImpactUnavailableReason.MissingLifecycleSupport
                when evidence.TenantStatus is TenantStatus.Unknown
                => "Tenants.Lifecycle.Unavailable.UnknownStatus",
            TenantHighImpactUnavailableReason.MissingLifecycleSupport => "Tenants.Lifecycle.Unavailable.CommandSurface",
            TenantHighImpactUnavailableReason.HighImpactFlowNotReady
                when evidence.Viewport is not TenantHighImpactViewportState.Safe
                => "Tenants.Lifecycle.Unavailable.Mobile",
            TenantHighImpactUnavailableReason.HighImpactFlowNotReady
                or TenantHighImpactUnavailableReason.MissingConsequencePreview
                or TenantHighImpactUnavailableReason.MissingAuditProof
                => "Tenants.Lifecycle.Unavailable.Governance",
            _ => "Tenants.Lifecycle.Available",
        };
    }

    private static ReadModelFreshnessState ToReadModelFreshness(TenantHighImpactActionEvidence evidence)
        => evidence.Freshness switch
        {
            TenantHighImpactFreshnessState.Current => ReadModelFreshnessState.Current,
            TenantHighImpactFreshnessState.Aging => ReadModelFreshnessState.Aging,
            TenantHighImpactFreshnessState.Stale => ReadModelFreshnessState.Stale,
            TenantHighImpactFreshnessState.Refreshing when evidence.HasCurrentBaseline
                => ReadModelFreshnessState.Current,
            _ => ReadModelFreshnessState.Unknown,
        };
}
