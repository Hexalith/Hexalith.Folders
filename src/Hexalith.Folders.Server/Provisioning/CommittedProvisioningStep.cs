namespace Hexalith.Folders.Server.Provisioning;

/// <summary>The fields used from the authenticated EventStore v1 lookup response.</summary>
internal sealed record CommittedProvisioningStep(
    string TenantId,
    string OriginalActorId,
    string AdmissionOperation,
    string RequestSha256,
    string TaskId,
    string StepKey,
    string? Receipt,
    bool AcceptsExecution);
