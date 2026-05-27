namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspacePathPolicyResult(
    bool IsAccepted,
    WorkspacePathPolicyDecision Decision,
    string? PathMetadataDigest,
    string? PathPolicyClass,
    string? UnsafePath)
{
    public static WorkspacePathPolicyResult Accepted(string pathMetadataDigest, string pathPolicyClass)
        => new(true, WorkspacePathPolicyDecision.Accepted, pathMetadataDigest, pathPolicyClass, UnsafePath: null);

    public static WorkspacePathPolicyResult Denied(WorkspacePathPolicyDecision decision)
        => new(false, decision, PathMetadataDigest: null, PathPolicyClass: null, UnsafePath: null);
}
