using System.Text.Json.Serialization;

namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Changed-path evidence for dirty-state diagnostics (<c>changedPathEvidence</c> wire object). Metadata-only:
/// only a digest or an opaque reference is ever surfaced — never raw paths, file contents, or diffs.
/// </summary>
/// <param name="EvidenceKind">Evidence kind (<c>digest</c>|<c>reference</c>|<c>redacted</c>|<c>unavailable</c>).</param>
/// <param name="Digest">Changed-path metadata digest, present only when <see cref="EvidenceKind"/> is <c>digest</c>.</param>
/// <param name="Reference">Opaque changed-path reference, present only when <see cref="EvidenceKind"/> is <c>reference</c>.</param>
/// <param name="Classification">Field classification (<c>consumer_safe</c>|<c>operator_sanitized</c>|<c>forbidden</c>).</param>
public sealed record ChangedPathEvidenceView(
    string EvidenceKind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Digest,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reference,
    string Classification);
