using System.Text.Json.Serialization;

namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Metadata-only failed-operation diagnostics view (<c>FailedOperationDiagnostics</c> wire shape).
/// Surfaces a sanitized error category, retry/escalation posture, and opaque correlation evidence only.
/// Lookup keys are never serialized.
/// </summary>
public sealed record FailedOperationDiagnosticsView(
    [property: JsonIgnore] string ManagedTenantId,
    [property: JsonIgnore] string FolderId,
    [property: JsonIgnore] string WorkspaceId,
    string Audience,
    string Status,
    string Disposition,
    DiagnosticTrustEvidenceView Trust,
    IReadOnlyList<DiagnosticFieldClassificationView> FieldClassifications,
    string OperationId,
    string TaskId,
    string SanitizedErrorCategory,
    RetryEligibilityView RetryEligibility,
    DiagnosticReadFreshness Freshness);
