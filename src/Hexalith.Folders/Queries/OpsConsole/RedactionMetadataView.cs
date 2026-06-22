namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Redaction metadata (<c>redaction</c> wire object). Redacted values are visibly distinct from missing
/// ones — a redacted identifier carries <c>visibility=redacted</c> and omits its value.
/// </summary>
/// <param name="Visibility">Redaction visibility (<c>metadata_only</c>|<c>redacted</c>).</param>
/// <param name="ReasonCode">Sanitized redaction reason code (<c>not_redacted</c> when visible).</param>
public sealed record RedactionMetadataView(
    string Visibility,
    string ReasonCode);
