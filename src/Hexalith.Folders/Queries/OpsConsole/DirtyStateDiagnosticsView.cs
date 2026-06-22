using System.Text.Json.Serialization;

namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Metadata-only dirty-state diagnostics view (<c>DirtyStateDiagnostics</c> wire shape). Changed-path
/// evidence is digest/reference only — never raw paths. Lookup keys are never serialized.
/// </summary>
public sealed record DirtyStateDiagnosticsView(
    [property: JsonIgnore] string ManagedTenantId,
    [property: JsonIgnore] string FolderId,
    [property: JsonIgnore] string WorkspaceId,
    string Audience,
    string Status,
    string Disposition,
    DiagnosticTrustEvidenceView Trust,
    IReadOnlyList<DiagnosticFieldClassificationView> FieldClassifications,
    ChangedPathEvidenceView ChangedPathEvidence,
    DiagnosticReadFreshness Freshness);
