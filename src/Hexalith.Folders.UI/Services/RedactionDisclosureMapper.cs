using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.UI.Services;

/// <summary>
/// Story 6.4 / F-5 — translates every SDK redaction wire vocabulary into a <see cref="FieldDisclosure"/>.
/// Each overload is <b>total</b> over its source enum and throws <see cref="ArgumentOutOfRangeException"/>
/// (offending value included) on an unrecognized member — <b>never</b> a silent default. If the OpenAPI
/// spine adds a redaction enum value, the unhandled-value throw turns the new value into a failing test
/// rather than a silent miss, which is the correct failure mode for a redaction surface.
/// </summary>
/// <remarks>
/// There is no server-side counterpart to drift against (the server emits the wire enums; the UI's job is
/// purely render-time interpretation), so unlike Story 6.3's <c>DispositionLabelMapper</c> there is no
/// cross-side parity test here — the protection is the totality <c>[Theory]</c> over each SDK enum.
/// <c>ProjectionAvailability</c> is intentionally NOT mapped: its <c>redacted</c>/<c>unknown</c> members are
/// <c>TODO(reference-pending C5)</c> in the generated client and must not be bound to an unapproved contract.
/// </remarks>
public static class RedactionDisclosureMapper
{
    /// <summary>Maps <see cref="RedactionMetadataVisibility"/> (audit-reference visibility) to a disclosure.</summary>
    public static FieldDisclosure FromAuditVisibility(RedactionMetadataVisibility visibility)
        => visibility switch
        {
            RedactionMetadataVisibility.Metadata_only => FieldDisclosure.Visible,
            RedactionMetadataVisibility.Redacted => FieldDisclosure.Redacted,
            _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Unknown redaction metadata visibility."),
        };

    /// <summary>Maps <see cref="RedactableAuditTimestampPrecision"/> to a disclosure. A bucketed timestamp is an aggregated disclosed value, not a redaction.</summary>
    public static FieldDisclosure FromTimestampPrecision(RedactableAuditTimestampPrecision precision)
        => precision switch
        {
            RedactableAuditTimestampPrecision.Exact => FieldDisclosure.Visible,
            RedactableAuditTimestampPrecision.Bucketed => FieldDisclosure.Visible,
            RedactableAuditTimestampPrecision.Redacted => FieldDisclosure.Redacted,
            _ => throw new ArgumentOutOfRangeException(nameof(precision), precision, "Unknown redactable audit timestamp precision."),
        };

    /// <summary>Maps <see cref="FileMetadataItemRedaction"/> to a disclosure. Excluded/binary-disallowed items have no value to render.</summary>
    public static FieldDisclosure FromFileMetadataRedaction(FileMetadataItemRedaction redaction)
        => redaction switch
        {
            FileMetadataItemRedaction.Not_redacted => FieldDisclosure.Visible,
            FileMetadataItemRedaction.Redacted => FieldDisclosure.Redacted,
            FileMetadataItemRedaction.Excluded => FieldDisclosure.Missing,
            FileMetadataItemRedaction.Binary_disallowed => FieldDisclosure.Missing,
            _ => throw new ArgumentOutOfRangeException(nameof(redaction), redaction, "Unknown file metadata item redaction."),
        };

    /// <summary>
    /// Maps <see cref="DiagnosticFieldClassification"/> to a disclosure. <c>Forbidden</c> renders as
    /// policy-hidden (F-5) regardless of value presence — must-not-appear is never rendered as missing.
    /// </summary>
    public static FieldDisclosure FromDiagnosticClassification(DiagnosticFieldClassification classification, bool hasValue)
        => classification switch
        {
            DiagnosticFieldClassification.Forbidden => FieldDisclosure.Redacted,
            DiagnosticFieldClassification.Consumer_safe => hasValue ? FieldDisclosure.Visible : FieldDisclosure.Missing,
            DiagnosticFieldClassification.Operator_sanitized => hasValue ? FieldDisclosure.Visible : FieldDisclosure.Missing,
            _ => throw new ArgumentOutOfRangeException(nameof(classification), classification, "Unknown diagnostic field classification."),
        };

    /// <summary>
    /// Convenience for the redactable audit references (<c>RedactableAuditActorReference</c>,
    /// <c>RedactableAuditOperationReference</c>, <c>RedactableDiagnosticIdentifier</c>) which each carry a
    /// <see cref="RedactionMetadata.Visibility"/> plus a nullable value. Redaction wins even if a value leaked in.
    /// </summary>
    public static FieldDisclosure FromAuditRedaction(RedactionMetadata redaction, string? value)
    {
        ArgumentNullException.ThrowIfNull(redaction);

        return redaction.Visibility == RedactionMetadataVisibility.Redacted
            ? FieldDisclosure.Redacted
            : string.IsNullOrEmpty(value) ? FieldDisclosure.Missing : FieldDisclosure.Visible;
    }
}
