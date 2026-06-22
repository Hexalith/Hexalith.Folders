namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Metadata-only projection-trust evidence (<c>trust</c> wire object) for a diagnostic view.
/// </summary>
/// <param name="Availability">Projection availability class (<c>available</c>|<c>stale</c>|<c>unavailable</c>|<c>redacted</c>|<c>unknown</c>).</param>
/// <param name="FreshnessAgeMilliseconds">Bounded projection age in milliseconds.</param>
/// <param name="StaleReasonCode">Sanitized stale reason code (<c>not_stale</c> when fresh).</param>
/// <param name="UnavailableReasonCode">Sanitized unavailable reason code (<c>none</c> when available).</param>
public sealed record DiagnosticTrustEvidenceView(
    string Availability,
    int FreshnessAgeMilliseconds,
    string StaleReasonCode,
    string UnavailableReasonCode);
