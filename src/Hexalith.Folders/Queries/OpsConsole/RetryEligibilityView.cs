namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Retry eligibility posture for failed-operation diagnostics (<c>retryEligibility</c> wire object).
/// </summary>
/// <param name="Eligible">Whether a retry is eligible.</param>
/// <param name="ReasonCode">Sanitized reason code for the retry posture.</param>
/// <param name="AdvisoryOnly">Whether the posture is advisory only (non-binding).</param>
public sealed record RetryEligibilityView(
    bool Eligible,
    string ReasonCode,
    bool AdvisoryOnly);
