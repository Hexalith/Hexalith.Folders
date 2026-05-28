namespace Hexalith.Folders.UI.Services;

/// <summary>
/// Story 6.6 / UX-DR9 — presentation classification for a Trust Matrix cell. A render-time vocabulary
/// (no <c>[EnumMember]</c>, never on the wire); the page derives it from the available SDK evidence
/// per dimension. <see cref="TrustDimensionStateMapper"/> maps it to a non-color-only label + slot.
/// </summary>
public enum TrustDimensionState
{
    /// <summary>The dimension is healthy / satisfied.</summary>
    Ready,

    /// <summary>A non-terminal concern is present (degraded-but-serving territory).</summary>
    Warning,

    /// <summary>A known, categorized failure requiring intervention.</summary>
    Failed,

    /// <summary>The underlying resource is known but unreachable (C6 <c>inaccessible</c>).</summary>
    Inaccessible,

    /// <summary>No evidence is available yet for this dimension.</summary>
    Unknown,

    /// <summary>Evidence exists but is older than the freshness target (freshness lag).</summary>
    Delayed,

    /// <summary>Supporting evidence for this dimension is withheld by tenant policy (F-5).</summary>
    Redacted,
}
