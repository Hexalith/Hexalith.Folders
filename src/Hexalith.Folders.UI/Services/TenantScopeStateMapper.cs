using Hexalith.Folders.Client.Generated;
using Hexalith.FrontComposer.Contracts.Attributes;

namespace Hexalith.Folders.UI.Services;

/// <summary>
/// Story 6.6 / UX-DR6 — resolves the SDK <see cref="EffectivePermissions"/> evidence into the
/// presentation-only <see cref="TenantAccessState"/> consumed by the Tenant Scope Banner, and maps
/// that state to a non-color-only label + <see cref="BadgeSlot"/>.
/// </summary>
/// <remarks>
/// The switch over the SDK <see cref="EffectivePermissionsAuthorizationOutcome"/> enum is total and
/// throws <see cref="ArgumentOutOfRangeException"/> on an unrecognized member — never a silent default —
/// so a new outcome member surfaces as a failing test rather than a banner that quietly mis-states the
/// authorization posture (a safe-denial correctness concern).
/// <para>
/// <b>Denial never confirms existence (UX-DR21):</b> a <see cref="EffectivePermissionsAuthorizationOutcome.Denied_safe"/>
/// outcome maps to <see cref="TenantAccessState.Denied"/> and the banner copy must not reveal whether
/// any folder/workspace behind the route exists.
/// </para>
/// </remarks>
public static class TenantScopeStateMapper
{
    /// <summary>
    /// Resolves the effective-access state from the permissions evidence. A <see langword="null"/>
    /// evidence object (not yet fetched / read model not answering) is <see cref="TenantAccessState.Unknown"/>.
    /// </summary>
    public static TenantAccessState Resolve(EffectivePermissions? permissions)
    {
        if (permissions is null)
        {
            return TenantAccessState.Unknown;
        }

        return permissions.AuthorizationOutcome switch
        {
            EffectivePermissionsAuthorizationOutcome.Denied_safe => TenantAccessState.Denied,
            EffectivePermissionsAuthorizationOutcome.Allowed => ResolveAllowed(permissions),
            _ => throw new ArgumentOutOfRangeException(
                nameof(permissions),
                permissions.AuthorizationOutcome,
                "Unknown effective-permissions authorization outcome."),
        };
    }

    /// <summary>Maps an access state to its badge slot (non-color-only appearance pairing).</summary>
    public static BadgeSlot ResolveSlot(TenantAccessState state)
        => state switch
        {
            TenantAccessState.Allowed => BadgeSlot.Success,
            TenantAccessState.Partial => BadgeSlot.Info,
            TenantAccessState.Denied => BadgeSlot.Danger,
            TenantAccessState.Redacted => BadgeSlot.Warning,
            TenantAccessState.Stale => BadgeSlot.Warning,
            TenantAccessState.Unknown => BadgeSlot.Neutral,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown tenant access state."),
        };

    /// <summary>Maps an access state to its operator-facing English label.</summary>
    public static string ResolveLabel(TenantAccessState state)
        => state switch
        {
            TenantAccessState.Allowed => "Access allowed",
            TenantAccessState.Partial => "Partial access",
            TenantAccessState.Denied => "Access denied",
            TenantAccessState.Redacted => "Access evidence redacted",
            TenantAccessState.Stale => "Access evidence stale",
            TenantAccessState.Unknown => "Access unknown",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown tenant access state."),
        };

    private static TenantAccessState ResolveAllowed(EffectivePermissions permissions)
    {
        // Freshness wins over the allowed/partial distinction: stale authorization evidence must be
        // labelled stale (UX-DR26) rather than presented as a current "allowed" without a freshness cue.
        if (permissions.Freshness?.Stale == true)
        {
            return TenantAccessState.Stale;
        }

        // Administer implies the full scope; anything less (or an empty allowed set) is partial.
        return permissions.Permissions is not null && permissions.Permissions.Contains(FolderPermissionLevel.Administer)
            ? TenantAccessState.Allowed
            : TenantAccessState.Partial;
    }
}
