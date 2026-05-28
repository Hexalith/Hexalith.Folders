using Hexalith.FrontComposer.Contracts.Attributes;

namespace Hexalith.Folders.UI.Services;

/// <summary>
/// Story 6.6 / UX-DR9 — maps a <see cref="TrustDimensionState"/> to its non-color-only label and
/// <see cref="BadgeSlot"/>. Total over the presentation enum; throws on an unrecognized member rather
/// than silently defaulting, so a new trust state cannot render as an unlabeled tile.
/// </summary>
public static class TrustDimensionStateMapper
{
    /// <summary>Maps a trust-dimension state to its badge slot.</summary>
    public static BadgeSlot ResolveSlot(TrustDimensionState state)
        => state switch
        {
            TrustDimensionState.Ready => BadgeSlot.Success,
            TrustDimensionState.Warning => BadgeSlot.Warning,
            TrustDimensionState.Failed => BadgeSlot.Danger,
            TrustDimensionState.Inaccessible => BadgeSlot.Danger,
            TrustDimensionState.Unknown => BadgeSlot.Neutral,
            TrustDimensionState.Delayed => BadgeSlot.Warning,
            TrustDimensionState.Redacted => BadgeSlot.Warning,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown trust dimension state."),
        };

    /// <summary>Maps a trust-dimension state to its operator-facing English label.</summary>
    public static string ResolveLabel(TrustDimensionState state)
        => state switch
        {
            TrustDimensionState.Ready => "Ready",
            TrustDimensionState.Warning => "Warning",
            TrustDimensionState.Failed => "Failed",
            TrustDimensionState.Inaccessible => "Inaccessible",
            TrustDimensionState.Unknown => "Unknown",
            TrustDimensionState.Delayed => "Delayed",
            TrustDimensionState.Redacted => "Redacted",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown trust dimension state."),
        };
}
