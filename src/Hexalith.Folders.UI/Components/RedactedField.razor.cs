using Hexalith.Folders.UI.Services;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components;

/// <summary>
/// Story 6.4 / F-5 — the redaction affordance component. Renders each <see cref="FieldDisclosure"/>
/// state distinctly: the redacted state shows a lock icon AND explanatory text (color/icon is never
/// the only signal); unknown and missing render distinct muted text with no lock. Text is always
/// present, so a redacted field never looks like a system defect (cross-cutting concern #11).
/// A pure inline renderer — no mutation, no tooltip/popover/dialog. Pages resolve the
/// <see cref="Disclosure"/> via <see cref="RedactionDisclosureMapper"/>.
/// </summary>
public partial class RedactedField : ComponentBase
{
    /// <summary>Default redacted explanatory copy (F-5 intent, architecture line 549).</summary>
    private const string DefaultRedactedExplanation = "Hidden by tenant policy — contact your administrator";

    /// <summary>Default unknown copy.</summary>
    private const string DefaultUnknownText = "Unknown";

    /// <summary>Default missing copy.</summary>
    private const string DefaultMissingText = "Not recorded";

    private string _disclosureToken = string.Empty;
    private string _displayText = string.Empty;
    private string? _ariaLabel;

    /// <summary>Gets or sets the disclosure state to render (primary input).</summary>
    [Parameter]
    [EditorRequired]
    public FieldDisclosure Disclosure { get; set; }

    /// <summary>
    /// Gets or sets the value, rendered <b>only</b> when <see cref="Disclosure"/> is
    /// <see cref="FieldDisclosure.Visible"/>. A redacted field must never leak its value even if a
    /// caller mistakenly passes one.
    /// </summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets an optional column-header label. When provided, the <c>aria-label</c> becomes
    /// <c>"{ColumnHeader}: {announced-text}"</c> (parallel to <c>FcStatusBadge</c>).
    /// </summary>
    [Parameter]
    public string? ColumnHeader { get; set; }

    /// <summary>Gets or sets an optional override for the redacted explanatory text; defaults to the F-5 copy.</summary>
    [Parameter]
    public string? RedactedExplanation { get; set; }

    /// <summary>Gets or sets an optional override for the unknown copy; defaults to <c>"Unknown"</c>.</summary>
    [Parameter]
    public string? UnknownText { get; set; }

    /// <summary>Gets or sets an optional override for the missing copy; defaults to <c>"Not recorded"</c>.</summary>
    [Parameter]
    public string? MissingText { get; set; }

    /// <summary>Gets or sets an optional CSS class appended to the outer wrapper.</summary>
    [Parameter]
    public string? AdditionalCssClass { get; set; }

    private bool IsRedacted => Disclosure == FieldDisclosure.Redacted;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        switch (Disclosure)
        {
            case FieldDisclosure.Visible:
                _disclosureToken = "visible";
                _displayText = Value ?? string.Empty;
                _ariaLabel = BuildAriaLabel(Value);
                break;
            case FieldDisclosure.Redacted:
                _disclosureToken = "redacted";
                _displayText = RedactedExplanation ?? DefaultRedactedExplanation;
                _ariaLabel = BuildAriaLabel(_displayText);
                break;
            case FieldDisclosure.Unknown:
                _disclosureToken = "unknown";
                _displayText = UnknownText ?? DefaultUnknownText;
                _ariaLabel = BuildAriaLabel(_displayText);
                break;
            case FieldDisclosure.Missing:
                _disclosureToken = "missing";
                _displayText = MissingText ?? DefaultMissingText;
                _ariaLabel = BuildAriaLabel(_displayText);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Disclosure), Disclosure, "Unknown field disclosure.");
        }
    }

    private string? BuildAriaLabel(string? announcedText)
    {
        // Per AC #4: when there is no announced text (a Visible field with no value), the aria-label
        // is omitted entirely. The non-visible states always carry non-empty copy, so they always announce.
        if (string.IsNullOrEmpty(announcedText))
        {
            return null;
        }

        return string.IsNullOrEmpty(ColumnHeader)
            ? announcedText
            : $"{ColumnHeader}: {announcedText}";
    }
}
