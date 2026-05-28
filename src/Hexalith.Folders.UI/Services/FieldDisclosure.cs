namespace Hexalith.Folders.UI.Services;

/// <summary>
/// Story 6.4 / F-5 — the presentation-level disclosure vocabulary the operations console uses to
/// render any audience-gated field. Every diagnostic surface maps the SDK's <em>field-specific</em>
/// redaction signals (<c>RedactionMetadataVisibility</c>, <c>DiagnosticFieldClassification</c>,
/// <c>RedactableAuditTimestampPrecision</c>, <c>FileMetadataItemRedaction</c>) into this single
/// rendering classification via <see cref="RedactionDisclosureMapper"/>, so redacted-vs-unknown-vs-missing
/// is rendered consistently in one place (cross-cutting concern #11).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a presentation concept, NOT a duplicate of an SDK wire enum.</b> Unlike Story 6.3's
/// <c>OperatorDispositionLabel</c> (which <em>is</em> the SDK wire vocabulary), there is no single SDK
/// enum for "visible / redacted / unknown / missing" — redaction is field-shaped across four distinct
/// SDK enums, each encoding it along its own axis. <see cref="FieldDisclosure"/> is the rendering
/// classification those axes collapse into (exactly as <c>BadgeSlot</c> collapses many dispositions
/// into five appearance slots). It therefore carries <b>no</b> <c>[EnumMember]</c> attribute and never
/// crosses the wire; centralizing it is what makes "redacted is visibly distinct from unknown/missing"
/// enforceable in one place.
/// </para>
/// </remarks>
public enum FieldDisclosure
{
    /// <summary>The value is disclosed to this audience and is rendered as-is.</summary>
    Visible,

    /// <summary>
    /// A tenant/audience policy hid the value (F-5). Rendered with a lock-icon affordance plus
    /// explanatory text; never silent truncation. MUST render distinctly from
    /// <see cref="Unknown"/> and <see cref="Missing"/>.
    /// </summary>
    Redacted,

    /// <summary>
    /// The value is not yet known to the read model (projection-pending / not-yet-observed).
    /// MUST render distinctly from <see cref="Redacted"/> and <see cref="Missing"/>.
    /// </summary>
    Unknown,

    /// <summary>
    /// No value exists for this field (not applicable / never recorded). MUST render distinctly
    /// from <see cref="Redacted"/> and <see cref="Unknown"/>.
    /// </summary>
    Missing,
}
