using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Services;
using Hexalith.FrontComposer.Contracts.Attributes;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components;

/// <summary>
/// Story 6.3 / F-4 — primary-visual operator-disposition badge. Thin domain-semantic wrapper
/// over <see cref="Hexalith.FrontComposer.Shell.Components.Badges.FcStatusBadge"/>: maps the
/// SDK-owned <see cref="OperatorDispositionLabel"/> to a FrontComposer <see cref="BadgeSlot"/>
/// + an English label via <see cref="DispositionLabelMapper"/> and forwards to the canonical
/// status badge so the slot table, aria-label template, and "color is never the only signal"
/// invariant are inherited rather than re-derived.
/// </summary>
public partial class OperatorDispositionBadge : ComponentBase
{
    private BadgeSlot _slot;
    private string _label = string.Empty;

    /// <summary>Gets or sets the operator disposition to render.</summary>
    [Parameter]
    [EditorRequired]
    public OperatorDispositionLabel Disposition { get; set; }

    /// <summary>
    /// Gets or sets the optional column-header text forwarded to the underlying
    /// <see cref="Hexalith.FrontComposer.Shell.Components.Badges.FcStatusBadge"/> so screen
    /// readers announce <c>"{ColumnHeader}: {Label}"</c>.
    /// </summary>
    [Parameter]
    public string? ColumnHeader { get; set; }

    /// <summary>Gets or sets an optional CSS class appended to the outer wrapper.</summary>
    [Parameter]
    public string? AdditionalCssClass { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _slot = DispositionLabelMapper.ResolveSlot(Disposition);
        _label = DispositionLabelMapper.ResolveLabel(Disposition);
    }
}
