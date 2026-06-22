namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Metadata-only freshness evidence for an ops-console diagnostic read. Serialized directly onto the
/// <c>freshness</c> wire object (<c>readConsistency</c>/<c>observedAt</c>/<c>projectionWatermark</c>/<c>stale</c>).
/// </summary>
/// <param name="ReadConsistency">Declared read-consistency class (always <c>eventually_consistent</c> for diagnostics).</param>
/// <param name="ObservedAt">When the diagnostic was observed.</param>
/// <param name="ProjectionWatermark">Opaque projection watermark, when known.</param>
/// <param name="Stale">Whether the backing projection is stale (within the safe-serving threshold).</param>
public sealed record DiagnosticReadFreshness(
    string ReadConsistency,
    DateTimeOffset ObservedAt,
    string? ProjectionWatermark,
    bool Stale);
