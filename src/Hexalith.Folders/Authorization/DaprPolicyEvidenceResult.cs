namespace Hexalith.Folders.Authorization;

public sealed record DaprPolicyEvidenceResult(
    DaprPolicyEvidenceStatus Status,
    string OutcomeCode,
    string TargetAppId,
    string? FreshnessWatermark,
    string FreshnessClass,
    bool Retryable)
{
    public static DaprPolicyEvidenceResult Allowed(string targetAppId, string? freshnessWatermark)
        => new(
            DaprPolicyEvidenceStatus.Allowed,
            LayeredAuthorizationOutcomeCodes.Allowed,
            targetAppId,
            freshnessWatermark,
            "fresh",
            Retryable: false);

    public static DaprPolicyEvidenceResult Denied(string targetAppId)
        => new(
            DaprPolicyEvidenceStatus.Denied,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied,
            targetAppId,
            FreshnessWatermark: null,
            FreshnessClass: "policy_denied",
            Retryable: false);

    public static DaprPolicyEvidenceResult Unavailable(string reasonCode)
        => new(
            DaprPolicyEvidenceStatus.Unavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied,
            TargetAppId: string.Empty,
            FreshnessWatermark: null,
            reasonCode,
            Retryable: true);

    public static DaprPolicyEvidenceResult Malformed()
        => new(
            DaprPolicyEvidenceStatus.Malformed,
            LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed,
            TargetAppId: string.Empty,
            FreshnessWatermark: null,
            FreshnessClass: "malformed",
            Retryable: false);
}
