using System.Text.Json.Serialization;

namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Metadata-only provider-status diagnostics view (<c>ProviderStatusDiagnostics</c> wire shape). The
/// provider binding is surfaced as a redactable opaque identifier; no credential reference or raw provider
/// payload is ever exposed. Lookup keys are never serialized.
/// </summary>
public sealed record ProviderStatusDiagnosticsView(
    [property: JsonIgnore] string ManagedTenantId,
    [property: JsonIgnore] string FolderId,
    string Audience,
    string Status,
    string Disposition,
    DiagnosticTrustEvidenceView Trust,
    IReadOnlyList<DiagnosticFieldClassificationView> FieldClassifications,
    RedactableDiagnosticIdentifierView ProviderBindingReference,
    string ProviderCorrelationReference,
    DiagnosticReadFreshness Freshness);
