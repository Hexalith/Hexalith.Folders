namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspacePathPolicyEvidenceResult(WorkspacePathPolicyEvidenceDecision Decision)
{
    /// <summary>Gets the immutable evaluated policy version.</summary>
    public string? PolicyVersion { get; init; }
    /// <summary>Gets the immutable evaluated policy digest.</summary>
    public string? PolicyDigest { get; init; }
}
