using Hexalith.Folders.UI.Components.Icons;
using Hexalith.Folders.UI.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components;

/// <summary>
/// Story 6.6 / UX-DR9 — compares the six trust dimensions (tenant boundary, provider readiness,
/// workspace lifecycle, lock state, folder metadata visibility, audit traceability) as <b>grouped
/// evidence</b>, not just visual tiles. Each cell carries a dimension name, a non-color-only state
/// (icon + badge text + color + accessible label), a reason summary, a last-updated time, and a link
/// to supporting evidence (UX-DR19). Rendered as a semantic table so the grouping is keyboard-reachable
/// and screen-reader meaningful.
/// </summary>
public partial class TrustMatrix : ComponentBase
{
    /// <summary>Gets or sets the trust dimension cells to render.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<Models.TrustMatrixCell> Cells { get; set; } = [];

    private static Icon ResolveIcon(TrustDimensionState state)
        => state switch
        {
            TrustDimensionState.Ready => FoldersConsoleIcons.CheckmarkCircle16(),
            TrustDimensionState.Warning => FoldersConsoleIcons.Warning16(),
            TrustDimensionState.Failed => FoldersConsoleIcons.ErrorCircle16(),
            TrustDimensionState.Inaccessible => FoldersConsoleIcons.ErrorCircle16(),
            TrustDimensionState.Unknown => FoldersConsoleIcons.Question16(),
            TrustDimensionState.Delayed => FoldersConsoleIcons.Clock16(),
            TrustDimensionState.Redacted => FoldersConsoleIcons.LockClosed16(),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown trust dimension state."),
        };
}
