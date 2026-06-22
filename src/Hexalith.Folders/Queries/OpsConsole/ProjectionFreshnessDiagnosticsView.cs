using System.Text.Json.Serialization;

namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Metadata-only projection-freshness diagnostics view (<c>ProjectionFreshnessDiagnostics</c> wire shape).
/// Unlike the other six diagnostics this does not derive from <c>DiagnosticBase</c> (no status/disposition/trust).
/// The <see cref="ManagedTenantId"/> lookup key is never serialized.
/// </summary>
public sealed record ProjectionFreshnessDiagnosticsView(
    [property: JsonIgnore] string ManagedTenantId,
    string Audience,
    string ProjectionName,
    string Availability,
    int FreshnessAgeMilliseconds,
    int ElapsedMilliseconds,
    string StaleReasonCode,
    string UnavailableReasonCode,
    string FreshnessTarget,
    IReadOnlyList<DiagnosticFieldClassificationView> FieldClassifications,
    DiagnosticReadFreshness Freshness);
