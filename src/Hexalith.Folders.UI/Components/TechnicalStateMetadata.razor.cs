using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Services;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components;

/// <summary>
/// Story 6.3 / F-4 — secondary metadata renderer for the underlying wire-level
/// <see cref="LifecycleState"/>. Renders the snake_case state name in muted Fluent UI
/// secondary typography so it visually de-emphasizes against the primary
/// <see cref="OperatorDispositionBadge"/> sibling.
/// </summary>
public partial class TechnicalStateMetadata : ComponentBase
{
    private string _wireName = string.Empty;
    private string _displayText = string.Empty;
    private string _ariaLabel = string.Empty;

    /// <summary>Gets or sets the technical lifecycle state to render.</summary>
    [Parameter]
    [EditorRequired]
    public LifecycleState State { get; set; }

    /// <summary>
    /// Gets or sets an optional column-header label. When provided, the rendered
    /// <c>aria-label</c> becomes <c>"{ColumnHeader}: {wire-name}"</c> so screen readers
    /// announce the surrounding context (parallel to <c>FcStatusBadge</c>'s pattern).
    /// </summary>
    [Parameter]
    public string? ColumnHeader { get; set; }

    /// <summary>
    /// Gets or sets whether the rendered text is prefixed with <c>"state: "</c>. Diagnostic
    /// pages can suppress the prefix in dense table cells by passing <see langword="false"/>.
    /// </summary>
    [Parameter]
    public bool IncludePrefix { get; set; } = true;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _wireName = DispositionLabelMapper.ResolveTechnicalStateLabel(State);
        _displayText = IncludePrefix ? $"state: {_wireName}" : _wireName;
        _ariaLabel = string.IsNullOrEmpty(ColumnHeader)
            ? _wireName
            : $"{ColumnHeader}: {_wireName}";
    }
}
