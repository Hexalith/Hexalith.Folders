using System.Text.Json.Serialization;

namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Metadata-only lock diagnostics view (<c>LockDiagnostics</c> wire shape). Lookup keys are never serialized.
/// </summary>
public sealed record LockDiagnosticsView(
    [property: JsonIgnore] string ManagedTenantId,
    [property: JsonIgnore] string FolderId,
    [property: JsonIgnore] string WorkspaceId,
    string Audience,
    string Status,
    string Disposition,
    DiagnosticTrustEvidenceView Trust,
    IReadOnlyList<DiagnosticFieldClassificationView> FieldClassifications,
    RedactableDiagnosticIdentifierView LockReference,
    DiagnosticReadFreshness Freshness);
