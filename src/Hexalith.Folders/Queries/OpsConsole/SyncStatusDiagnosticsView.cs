using System.Text.Json.Serialization;

namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Metadata-only sync-status diagnostics view (<c>SyncStatusDiagnostics</c> wire shape). Separates accepted
/// command state, projected C6 state, and provider-outcome state so operators can distinguish projection lag
/// from genuine provider divergence. Lookup keys are never serialized.
/// </summary>
public sealed record SyncStatusDiagnosticsView(
    [property: JsonIgnore] string ManagedTenantId,
    [property: JsonIgnore] string FolderId,
    [property: JsonIgnore] string WorkspaceId,
    string Audience,
    string Status,
    string Disposition,
    DiagnosticTrustEvidenceView Trust,
    IReadOnlyList<DiagnosticFieldClassificationView> FieldClassifications,
    string AcceptedCommandState,
    string ProjectedState,
    string ProviderOutcomeState,
    DiagnosticReadFreshness Freshness);
