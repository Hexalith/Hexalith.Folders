using System.Text.Json.Serialization;

namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Audience-gated, metadata-only diagnostic identifier (<c>RedactableDiagnosticIdentifier</c> wire object).
/// The <see cref="Value"/> is omitted when the identifier is redacted (the redacted-implies-no-value invariant).
/// </summary>
/// <param name="Value">Opaque identifier value; <c>null</c> (omitted) when redacted.</param>
/// <param name="Classification">Field classification (<c>consumer_safe</c>|<c>operator_sanitized</c>|<c>forbidden</c>).</param>
/// <param name="Redaction">Redaction metadata.</param>
public sealed record RedactableDiagnosticIdentifierView(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Value,
    string Classification,
    RedactionMetadataView Redaction);
