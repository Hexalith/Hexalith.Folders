using Hexalith.Folders.UI.Services;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components;

/// <summary>
/// Story 6.6 / §3.8 — renders one of the four distinct, reason-labelled empty states without leaking
/// unauthorized resource existence (UX-DR20/21). Text-first; the reason token is exposed as
/// <c>data-fc-empty-reason</c> for tests and assistive-tech context.
/// </summary>
public partial class ConsoleEmptyState : ComponentBase
{
    private string _reasonToken = string.Empty;
    private string _heading = string.Empty;
    private string _body = string.Empty;

    /// <summary>Gets or sets the distinct empty-state reason to render.</summary>
    [Parameter]
    [EditorRequired]
    public EmptyStateReason Reason { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        (_reasonToken, _heading, _body) = Reason switch
        {
            EmptyStateReason.NoMatches => (
                "no_matches",
                "No matches",
                "The query is valid but returned no results."),
            EmptyStateReason.InsufficientFilterScope => (
                "insufficient_filter_scope",
                "Narrow your scope",
                "Provide a folder or workspace identifier to scope this diagnostic view."),
            EmptyStateReason.ReadModelUnavailable => (
                "read_model_unavailable",
                "Read model unavailable",
                "The projection is currently unavailable and cannot answer this query."),
            EmptyStateReason.DeniedAccess => (
                "denied_access",
                "Access denied",
                "Access was denied. This does not confirm whether any resource exists."),
            _ => throw new ArgumentOutOfRangeException(nameof(Reason), Reason, "Unknown empty-state reason."),
        };
    }
}
