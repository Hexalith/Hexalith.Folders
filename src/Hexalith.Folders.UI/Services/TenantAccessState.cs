namespace Hexalith.Folders.UI.Services;

/// <summary>
/// Story 6.6 / UX-DR6 — presentation classification for the Tenant Scope Banner's effective-access
/// indicator. This is a <b>render-time</b> vocabulary (like <see cref="FieldDisclosure"/>), not an SDK
/// wire enum: the SDK exposes only <c>EffectivePermissionsAuthorizationOutcome</c>
/// (<c>allowed</c>/<c>denied_safe</c>) plus a permission set and a freshness flag, which
/// <see cref="TenantScopeStateMapper"/> collapses into the six operator-visible access states the
/// UX-DR6 anatomy enumerates. Carrying no <c>[EnumMember]</c>, it never crosses the wire.
/// </summary>
public enum TenantAccessState
{
    /// <summary>Authorization succeeded and the principal holds full administrative scope.</summary>
    Allowed,

    /// <summary>
    /// Safe denial (UX-DR21). The banner says access is denied <b>without</b> confirming that any
    /// underlying resource exists.
    /// </summary>
    Denied,

    /// <summary>Authorization succeeded but the principal holds a reduced permission set.</summary>
    Partial,

    /// <summary>The effective-access evidence is itself withheld by tenant policy (F-5).</summary>
    Redacted,

    /// <summary>No authorization evidence is available yet (projection-pending / not observed).</summary>
    Unknown,

    /// <summary>Authorization evidence exists but is older than the freshness target (UX-DR26).</summary>
    Stale,
}
