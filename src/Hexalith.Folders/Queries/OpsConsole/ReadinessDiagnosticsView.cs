using System.Text.Json.Serialization;

namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Metadata-only readiness diagnostics view (<c>ReadinessDiagnostics</c> wire shape). The
/// <see cref="ManagedTenantId"/> lookup key is never serialized.
/// </summary>
public sealed record ReadinessDiagnosticsView(
    [property: JsonIgnore] string ManagedTenantId,
    string Audience,
    string Status,
    string Disposition,
    DiagnosticTrustEvidenceView Trust,
    IReadOnlyList<DiagnosticFieldClassificationView> FieldClassifications,
    RedactableDiagnosticIdentifierView ProviderSummaryReference,
    RedactableDiagnosticIdentifierView FolderSummaryReference,
    RedactableDiagnosticIdentifierView WorkspaceSummaryReference,
    DiagnosticReadFreshness Freshness);
