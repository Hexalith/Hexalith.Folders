using Hexalith.Folders.UI.Components.Models;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components;

/// <summary>
/// Story 6.6 / §3.9 — renders a metadata-only safe-denial / safe-error envelope: reason category →
/// safe explanation → correlation-ID evidence (monospace safe-copy) → escalation posture. Never a
/// stack trace, secret, or resource-existence oracle.
/// </summary>
public partial class ConsoleErrorPanel : ComponentBase
{
    /// <summary>Gets or sets the safe-error view to render.</summary>
    [Parameter]
    [EditorRequired]
    public ConsoleErrorView Error { get; set; } = default!;
}
