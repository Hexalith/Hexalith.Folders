using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.UI.Services;

/// <summary>
/// Story 6.6 / UX-DR9 — derives a <see cref="TrustDimensionState"/> for each Trust Matrix dimension from
/// the available SDK evidence. Every switch over an SDK enum is total and throws on an unrecognized
/// member; a <see langword="null"/> input (evidence not fetched / read model silent) is
/// <see cref="TrustDimensionState.Unknown"/> — never a silently-defaulted "ready" tile.
/// </summary>
public static class TrustDimensionDeriver
{
    /// <summary>Tenant-boundary dimension from the effective-access posture.</summary>
    public static TrustDimensionState FromAuthorization(TenantAccessState access)
        => access switch
        {
            TenantAccessState.Allowed => TrustDimensionState.Ready,
            TenantAccessState.Partial => TrustDimensionState.Warning,
            TenantAccessState.Denied => TrustDimensionState.Failed,
            TenantAccessState.Redacted => TrustDimensionState.Redacted,
            TenantAccessState.Stale => TrustDimensionState.Delayed,
            TenantAccessState.Unknown => TrustDimensionState.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(access), access, "Unknown tenant access state."),
        };

    /// <summary>Workspace-lifecycle dimension from the operator disposition.</summary>
    public static TrustDimensionState FromDisposition(OperatorDispositionLabel disposition)
        => disposition switch
        {
            OperatorDispositionLabel.Available => TrustDimensionState.Ready,
            OperatorDispositionLabel.Auto_recovering => TrustDimensionState.Warning,
            OperatorDispositionLabel.Degraded_but_serving => TrustDimensionState.Warning,
            OperatorDispositionLabel.Awaiting_human => TrustDimensionState.Warning,
            OperatorDispositionLabel.Terminal_until_intervention => TrustDimensionState.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, "Unknown operator disposition label."),
        };

    /// <summary>Lock-state dimension; <see langword="null"/> when the lock evidence was not fetched.</summary>
    public static TrustDimensionState FromLockState(LockState? lockState)
        => lockState switch
        {
            null => TrustDimensionState.Unknown,
            LockState.Unlocked => TrustDimensionState.Ready,
            LockState.Locked => TrustDimensionState.Warning,
            LockState.Expired => TrustDimensionState.Warning,
            LockState.Stale => TrustDimensionState.Delayed,
            LockState.Revoked => TrustDimensionState.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(lockState), lockState, "Unknown lock state."),
        };

    /// <summary>Provider-readiness dimension; <see langword="null"/> when the provider outcome was not fetched.</summary>
    public static TrustDimensionState FromProviderOutcome(ProviderOutcomeState? outcome)
        => outcome switch
        {
            null => TrustDimensionState.Unknown,
            ProviderOutcomeState.Known_success => TrustDimensionState.Ready,
            ProviderOutcomeState.Pending => TrustDimensionState.Warning,
            ProviderOutcomeState.Known_failure => TrustDimensionState.Failed,
            ProviderOutcomeState.Unknown_provider_outcome => TrustDimensionState.Warning,
            ProviderOutcomeState.Reconciliation_required => TrustDimensionState.Warning,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown provider outcome state."),
        };
}
