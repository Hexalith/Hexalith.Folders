namespace Hexalith.Folders.Authorization;

public sealed class DaprPolicyEvidenceOptions
{
    public const string SectionName = "Folders:Authorization:DaprPolicy";

    public bool RequirePolicyEvidence { get; set; }

    public bool Enabled { get; set; }

    public string[] AllowedTargetAppIds { get; set; } = ["folders"];

    public string[] AllowedServiceInvocationClasses { get; set; } = ["domain_service", "query", "diagnostic_read"];
}
