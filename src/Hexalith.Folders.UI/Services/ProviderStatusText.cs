using Hexalith.Folders.Client.Generated;
using Hexalith.FrontComposer.Contracts.Attributes;

namespace Hexalith.Folders.UI.Services;

/// <summary>
/// Story 6.7 — render-time display resolvers for the provider SDK status enums the Provider view
/// (§3.3) and the provider-support capability matrix (FR57) surface: capability support state,
/// repository binding state, provider readiness status, provider outcome state, capability name, and
/// sensitive-metadata tier. Every switch is <b>total</b> over its SDK enum and throws
/// <see cref="ArgumentOutOfRangeException"/> on an unrecognized member — never a silent default — so a
/// contract drift becomes a failing totality test rather than a mis-rendered status (the same C6/F-4
/// correctness discipline as <see cref="ConsoleStatusText"/>). The status enums that carry a badge
/// (capability support, repository binding, readiness, provider outcome) resolve to a
/// <see cref="BadgeSlot"/> so they render text + icon/shape + semantic color + label (UX-DR14), never a
/// bare color span. Label-producing members return <see cref="string"/> so a later
/// <c>IStringLocalizer</c> wrapper is a pure refactor (FR localization deferred to Story 6.11).
/// </summary>
/// <remarks>
/// The <c>unknown_provider_outcome</c> / <c>reconciliation_required</c> members (on both
/// <see cref="ProviderOutcomeState"/> and <see cref="RepositoryBindingBindingState"/>) resolve to the
/// <see cref="BadgeSlot.Warning"/> slot — the <c>awaiting-human</c> disposition treatment — so they are
/// never rendered as a neutral "Unknown" with no badge (wireflow §2.2 / §2.3, AC #8). The server-computed
/// <see cref="ProviderStatusDiagnostics.Disposition"/> remains the authoritative primary readiness visual;
/// these per-state badges are secondary metadata only.
/// </remarks>
public static class ProviderStatusText
{
    /// <summary>Maps a provider capability support state to its operator-facing label (FR57).</summary>
    public static string ResolveCapabilityStateLabel(ProviderCapabilityState state)
        => state switch
        {
            ProviderCapabilityState.Supported => "Supported",
            ProviderCapabilityState.Unsupported => "Unsupported",
            ProviderCapabilityState.Temporarily_unavailable => "Temporarily unavailable",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown provider capability state."),
        };

    /// <summary>Maps a provider capability support state to its badge slot (non-color-only, UX-DR14).</summary>
    public static BadgeSlot ResolveCapabilityStateSlot(ProviderCapabilityState state)
        => state switch
        {
            ProviderCapabilityState.Supported => BadgeSlot.Success,
            ProviderCapabilityState.Unsupported => BadgeSlot.Danger,
            ProviderCapabilityState.Temporarily_unavailable => BadgeSlot.Warning,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown provider capability state."),
        };

    /// <summary>Maps a repository binding state to its operator-facing label.</summary>
    public static string ResolveBindingStateLabel(RepositoryBindingBindingState state)
        => state switch
        {
            RepositoryBindingBindingState.Requested => "Requested",
            RepositoryBindingBindingState.Bound => "Bound",
            RepositoryBindingBindingState.Failed => "Failed",
            RepositoryBindingBindingState.Unknown_provider_outcome => "Unknown provider outcome",
            RepositoryBindingBindingState.Reconciliation_required => "Reconciliation required",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown repository binding state."),
        };

    /// <summary>
    /// Maps a repository binding state to its badge slot. <c>unknown_provider_outcome</c> and
    /// <c>reconciliation_required</c> resolve to <see cref="BadgeSlot.Warning"/> (the awaiting-human
    /// treatment), <c>failed</c> to <see cref="BadgeSlot.Danger"/>, <c>bound</c> to
    /// <see cref="BadgeSlot.Success"/>, <c>requested</c> to <see cref="BadgeSlot.Info"/> (in-flight).
    /// </summary>
    public static BadgeSlot ResolveBindingStateSlot(RepositoryBindingBindingState state)
        => state switch
        {
            RepositoryBindingBindingState.Requested => BadgeSlot.Info,
            RepositoryBindingBindingState.Bound => BadgeSlot.Success,
            RepositoryBindingBindingState.Failed => BadgeSlot.Danger,
            RepositoryBindingBindingState.Unknown_provider_outcome => BadgeSlot.Warning,
            RepositoryBindingBindingState.Reconciliation_required => BadgeSlot.Warning,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown repository binding state."),
        };

    /// <summary>Maps a provider outcome state to its operator-facing label (advisory failure metadata).</summary>
    public static string ResolveOutcomeStateLabel(ProviderOutcomeState state)
        => state switch
        {
            ProviderOutcomeState.Pending => "Pending",
            ProviderOutcomeState.Known_success => "Known success",
            ProviderOutcomeState.Known_failure => "Known failure",
            ProviderOutcomeState.Unknown_provider_outcome => "Unknown provider outcome",
            ProviderOutcomeState.Reconciliation_required => "Reconciliation required",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown provider outcome state."),
        };

    /// <summary>
    /// Maps a provider outcome state to its badge slot. <c>unknown_provider_outcome</c> and
    /// <c>reconciliation_required</c> resolve to <see cref="BadgeSlot.Warning"/> (awaiting-human, AC #8) —
    /// never a neutral unbadged "Unknown".
    /// </summary>
    public static BadgeSlot ResolveOutcomeStateSlot(ProviderOutcomeState state)
        => state switch
        {
            ProviderOutcomeState.Pending => BadgeSlot.Info,
            ProviderOutcomeState.Known_success => BadgeSlot.Success,
            ProviderOutcomeState.Known_failure => BadgeSlot.Danger,
            ProviderOutcomeState.Unknown_provider_outcome => BadgeSlot.Warning,
            ProviderOutcomeState.Reconciliation_required => BadgeSlot.Warning,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown provider outcome state."),
        };

    /// <summary>
    /// Maps a provider readiness status to its operator-facing label. NOTE: this label/slot pair is
    /// <b>not</b> a <c>ProviderReadinessStatus</c>-to-disposition mapping (forbidden by the Dev Notes —
    /// the server <see cref="ProviderStatusDiagnostics.Disposition"/> is authoritative). The enum is only
    /// returned by the out-of-scope active validations probe (<c>ProviderReadinessConsumer/Operator</c>),
    /// so it is not rendered on the passive read path; the mapper exists for contract/totality parity.
    /// </summary>
    public static string ResolveReadinessStatusLabel(ProviderReadinessStatus status)
        => status switch
        {
            ProviderReadinessStatus.Ready => "Ready",
            ProviderReadinessStatus.Degraded => "Degraded",
            ProviderReadinessStatus.Failed => "Failed",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown provider readiness status."),
        };

    /// <summary>Maps a provider readiness status to its badge slot. See <see cref="ResolveReadinessStatusLabel"/>.</summary>
    public static BadgeSlot ResolveReadinessStatusSlot(ProviderReadinessStatus status)
        => status switch
        {
            ProviderReadinessStatus.Ready => BadgeSlot.Success,
            ProviderReadinessStatus.Degraded => BadgeSlot.Warning,
            ProviderReadinessStatus.Failed => BadgeSlot.Danger,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown provider readiness status."),
        };

    /// <summary>Maps a provider capability name to its operator-facing display label (FR57).</summary>
    public static string ResolveCapabilityNameLabel(ProviderCapabilityName capability)
        => capability switch
        {
            ProviderCapabilityName.Repository_creation => "Repository creation",
            ProviderCapabilityName.Existing_repository_binding => "Existing repository binding",
            ProviderCapabilityName.Branch_ref_policy => "Branch & ref policy",
            ProviderCapabilityName.File_operations => "File operations",
            ProviderCapabilityName.Commit_status => "Commit status",
            ProviderCapabilityName.Provider_errors => "Provider errors",
            ProviderCapabilityName.Failure_behavior => "Failure behavior",
            _ => throw new ArgumentOutOfRangeException(nameof(capability), capability, "Unknown provider capability name."),
        };

    /// <summary>Maps a sensitive-metadata tier to its operator-facing label (concern #17).</summary>
    public static string ResolveSensitiveMetadataTierLabel(SensitiveMetadataTier tier)
        => tier switch
        {
            SensitiveMetadataTier.Public_metadata => "Public metadata",
            SensitiveMetadataTier.Tenant_sensitive => "Tenant-sensitive",
            SensitiveMetadataTier.Credential_sensitive => "Credential-sensitive",
            SensitiveMetadataTier.Secret => "Secret",
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown sensitive metadata tier."),
        };
}
