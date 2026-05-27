namespace Hexalith.Folders.Queries.FileContext;

public sealed record WorkspacePathSensitivityResult(
    WorkspaceFileSensitivityDecision Decision,
    string Sensitivity,
    string Redaction)
{
    public static WorkspacePathSensitivityResult Allowed(string sensitivity = "tenant_sensitive")
        => new(WorkspaceFileSensitivityDecision.Allowed, sensitivity, "not_redacted");

    public static WorkspacePathSensitivityResult Redacted(string sensitivity = "restricted")
        => new(WorkspaceFileSensitivityDecision.Redacted, sensitivity, "redacted");

    public static WorkspacePathSensitivityResult Unavailable()
        => new(WorkspaceFileSensitivityDecision.Unavailable, "unknown", "redacted");
}
