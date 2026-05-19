namespace Hexalith.Folders.Authorization;

public sealed record DaprPolicyEvidenceRequest(
    string TargetAppId,
    string ServiceInvocationClass,
    bool RequiresPolicyEvidence,
    string? CorrelationId,
    string? TaskId);
